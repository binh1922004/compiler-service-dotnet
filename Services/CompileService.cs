using System.Text.Json;
using CompilerService.Configuration;
using CompilerService.Infrastructure.Docker;
using CompilerService.Infrastructure.Storage;
using CompilerService.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;

namespace CompilerService.Services;

public class CompileService(
    DockerPool dockerPool,
    IFileService fileService,
    IOptions<WorkSettings> workSettings,
    CommandBuilder commandBuilder,
    IS3Service s3Service,
    ILogger<CompileService> logger) : ICompileService
{
    private readonly WorkSettings _workSettings = workSettings.Value;
    
    public async Task<SubmissionResponse?> SubmitCode(SubmissionRequest submissionRequest,
        CancellationToken cancellationToken)
    {
        var containerId = await dockerPool.RentContainerAsync();
        try
        {
            var problemPath = Path.Combine(_workSettings.ProblemDir, submissionRequest.Problem.Id);
            if (!fileService.FolderExists(problemPath))
            {
                await s3Service.DownloadProblemFromS3Async(submissionRequest.Problem.Id, problemPath);
            }

            await CreateFile(submissionRequest, containerId!, cancellationToken);
            var judgeOutput = await JudgeCode(submissionRequest, containerId!, cancellationToken);
            logger.LogInformation("Judge output for submission {SubmissionId}: {Output}", submissionRequest.Id, judgeOutput);
            var jsonObject = JsonSerializer.Deserialize<SubmissionResponse>(judgeOutput);
            if (jsonObject != null)
            {
                jsonObject.Id = submissionRequest.Id;
            }
            return jsonObject;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error submitting code for submission {SubmissionId}", submissionRequest.Id);
            return null;
        }
        finally
        {
            await dockerPool.ReturnContainerAsync(containerId!, cancellationToken: cancellationToken);
        }
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
        var cmdCreateFile = commandBuilder.CreateSourceFileCommand(submissionRequest.Source, submissionRequest.Id, extension);
        await dockerPool.ExecCmdFromContainer(containerId, cmdCreateFile, cancellationToken);
    }
}