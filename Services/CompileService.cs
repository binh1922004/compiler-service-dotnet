using System.Text.Json;
using CompilerService.Configuration;
using CompilerService.Infrastructure.Docker;
using CompilerService.Infrastructure.Storage;
using CompilerService.Models;
using Microsoft.Extensions.Options;

namespace CompilerService.Services;

public class CompileService(
    DockerPool dockerPool,
    IFileService fileService,
    IOptions<WorkSettings> workSettings,
    CommandBuilder commandBuilder,
    IS3Service s3Service,
    IOptions<AwsS3Settings> awsS3Settings,
    ILogger<CompileService> logger) : ICompileService
{
    private readonly WorkSettings _workSettings = workSettings.Value;
    private readonly AwsS3Settings _awsS3Settings = awsS3Settings.Value;

    public async Task<SubmissionResponse?> SubmitCode(SubmissionRequest submissionRequest,
        CancellationToken cancellationToken)
    {
        var containerId = await dockerPool.RentContainerAsync();
        logger.LogInformation("Rented container {ContainerId} for submission {SubmissionId}", containerId,
            submissionRequest.Id);
        try
        {
            var version = submissionRequest.Problem.Version == 0 ? "" : $"-v{submissionRequest.Problem.Version}";
            var problemVersion = submissionRequest.Problem.Id + version;
            var problemPath = Path.Combine(_workSettings.ProblemDir, problemVersion);
            if (!fileService.FolderExists(problemPath))
            {
                var key = $"{submissionRequest.Problem.Id}/{problemVersion}.zip";
                await s3Service.DownloadProblemFromS3Async(key, problemPath);
            }

            await CreateFile(submissionRequest, containerId!, cancellationToken);
            var judgeOutput = await JudgeCode(submissionRequest, containerId!, cancellationToken);
            logger.LogInformation("Judge output for submission {SubmissionId}: {Output}", submissionRequest.Id,
                judgeOutput);
            var jsonObject = JsonSerializer.Deserialize<SubmissionResponse>(judgeOutput);
            if (jsonObject != null) jsonObject.Id = submissionRequest.Id;
            return jsonObject;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error submitting code for submission {SubmissionId}", submissionRequest.Id);
            return null;
        }
        finally
        {
            await DeleteSubmissionFolder(submissionRequest, containerId!, cancellationToken);
            await dockerPool.ReturnContainerAsync(containerId!, cancellationToken);
        }
    }

    public async Task<TestCaseGenerationResult> GenerateTestCases(TestCasePlan plan,
        CancellationToken cancellationToken)
    {
        var containerId = await dockerPool.RentContainerAsync();
        logger.LogInformation("Rented container {ContainerId} for test case generation, PlanId={PlanId}",
            containerId, plan.PlanId);

        try
        {
            var planName = $"{plan.PlanId}-v{plan.Version}";
            // 1. Create the working directory and write input.py + output.py
            var createFilesCmd = commandBuilder.CreateTestCaseFilesCommand(planName, plan.InputCode, plan.OutPutCode);
            await dockerPool.ExecCmdFromContainer(containerId!, createFilesCmd, cancellationToken);

            // 2. Run the test_case_generator.py script (capture both stdout and stderr)
            var generateCmd = commandBuilder.GenerateTestCaseCommand(planName);
            logger.LogInformation("Executing test case generation command: {Command}", generateCmd);
            var (rawOutput, rawStderr) =
                await dockerPool.ExecCmdFromContainerWithStderr(containerId!, generateCmd, cancellationToken);

            logger.LogInformation("Test case generator stdout for PlanId={PlanId}: {Output}",
                plan.PlanId, rawOutput);
            if (!string.IsNullOrWhiteSpace(rawStderr))
                logger.LogWarning("Test case generator stderr for PlanId={PlanId}: {Stderr}",
                    plan.PlanId, rawStderr);

            // 3. Parse the JSON output from the script
            var scriptOutput = ParseScriptOutput(rawOutput);

            if (scriptOutput == null)
            {
                logger.LogError("Failed to parse test case generator output for PlanId={PlanId}", plan.PlanId);
                return new TestCaseGenerationResult
                {
                    PlanId = plan.PlanId,
                    Success = false,
                    Version = plan.Version,
                    Error = $"Failed to parse script output.\nstdout: {rawOutput}\nstderr: {rawStderr}"
                };
            }

            if (!scriptOutput.IsSuccess)
            {
                var errorDetail = BuildDetailedError(scriptOutput, rawStderr);
                logger.LogWarning("Test case generation failed for PlanId={PlanId}: {Error}",
                    plan.PlanId, errorDetail);
                return new TestCaseGenerationResult
                {
                    PlanId = plan.PlanId,
                    Success = false,
                    Version = plan.Version,
                    Error = errorDetail
                };
            }

            logger.LogInformation(
                "Test case generation succeeded for PlanId={PlanId}. TestCount={TestCount}, ZipPath={ZipPath}",
                plan.PlanId, scriptOutput.TestCount, scriptOutput.ZipPath);
            var s3Key = $"{_awsS3Settings.TestCasePrefix}/{planName}/test_cases.zip";
            var s3UploadResult = await s3Service.UploadFileAsync("/app" + scriptOutput.ZipPath, s3Key);
            return new TestCaseGenerationResult
            {
                PlanId = plan.PlanId,
                Success = true,
                S3Key = s3Key,
                Version = plan.Version,
                TestCount = scriptOutput.TestCount
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error generating test cases for PlanId={PlanId}", plan.PlanId);
            return new TestCaseGenerationResult
            {
                PlanId = plan.PlanId,
                Success = false,
                Version = plan.Version,
                Error = ex.Message
            };
        }
        finally
        {
            // Clean up the test-case folder inside the container
            var deleteCmd = commandBuilder.DeleteTestCaseFolderCommand(plan.PlanId);
            await dockerPool.ExecCmdFromContainer(containerId!, deleteCmd, cancellationToken);
            await dockerPool.ReturnContainerAsync(containerId!, cancellationToken);
        }
    }

    private TestCaseScriptOutput? ParseScriptOutput(string rawOutput)
    {
        try
        {
            // The script outputs a single JSON line on stdout.
            // Docker exec output may contain stderr mixed in, so find the JSON line.
            var lines = rawOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("{") && trimmed.EndsWith("}"))
                    return JsonSerializer.Deserialize<TestCaseScriptOutput>(trimmed);
            }

            return null;
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "JSON parse error for script output: {Output}", rawOutput);
            return null;
        }
    }

    /// <summary>
    /// Builds a detailed, human-readable error string from the script's structured output.
    /// </summary>
    private static string BuildDetailedError(TestCaseScriptOutput scriptOutput, string dockerStderr)
    {
        var parts = new List<string>();

        // Phase info (input_generation / output_generation)
        if (!string.IsNullOrWhiteSpace(scriptOutput.Phase))
            parts.Add($"[Phase: {scriptOutput.Phase}]");

        // Error type (RuntimeError, FileNotFoundError, etc.)
        if (!string.IsNullOrWhiteSpace(scriptOutput.ErrorType))
            parts.Add($"[Type: {scriptOutput.ErrorType}]");

        // The main error message (includes stderr from subprocess, input preview, etc.)
        if (!string.IsNullOrWhiteSpace(scriptOutput.Error))
            parts.Add(scriptOutput.Error);

        // Python traceback from the script itself
        if (!string.IsNullOrWhiteSpace(scriptOutput.Traceback))
            parts.Add($"--- Python Traceback ---\n{scriptOutput.Traceback}");
        
        return string.Join("\n", parts);
    }

    private async Task<string> JudgeCode(SubmissionRequest submissionRequest, string containerId,
        CancellationToken cancellationToken)
    {
        var execCmd = commandBuilder.CreateCompileCommand(submissionRequest);
        return await dockerPool.ExecCmdFromContainer(containerId, execCmd, cancellationToken);
    }

    private async Task CreateFile(SubmissionRequest submissionRequest, string containerId,
        CancellationToken cancellationToken)
    {
        var extension = Constants.GetLanguageExtension(submissionRequest.Language);
        var cmdCreateFile =
            commandBuilder.CreateSourceFileCommand(submissionRequest.Source, submissionRequest.Id, extension);
        await dockerPool.ExecCmdFromContainer(containerId, cmdCreateFile, cancellationToken);
    }

    private async Task DeleteSubmissionFolder(SubmissionRequest submissionRequest, string containerId,
        CancellationToken cancellationToken)
    {
        var extension = Constants.GetLanguageExtension(submissionRequest.Language);
        var cmdDeleteFile = commandBuilder.CreateDeleteSubmissionFolderCommand(submissionRequest.Id);
        await dockerPool.ExecCmdFromContainer(containerId, cmdDeleteFile, cancellationToken);
    }
}