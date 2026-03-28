using CompilerService.Commands;
using CompilerService.Docker;
using CompilerService.DTO;
using CompilerService.Settings;
using CompilerService.Utilities;
using Microsoft.Extensions.Options;

namespace CompilerService.Services;

public class CompileService(
    DockerPool dockerPool,
    IFileService fileService,
    IOptions<WorkSetting> workSetting,
    FileCommand fileCommand,
    IS3Service s3Service,
    CompilerCommand compilerCommand) : ICompileService
{
    private readonly WorkSetting _workSetting = workSetting.Value;
    public async Task SubmitCode(SubmissionRequest submissionRequest, CancellationToken cancellationToken)
    {
        var containerId = await dockerPool.RentContainerAsync();
        try
        {
            var problemPath = Path.Combine(_workSetting.ProblemDir, submissionRequest.Problem.Id);
            if (!fileService.FolderExists(problemPath))
            {
                await s3Service.DownloadProblemFromS3Async(submissionRequest.Problem.Id, problemPath);
            }

            await CreateFile(submissionRequest, containerId, cancellationToken);
            await JudgeCode(submissionRequest, containerId, cancellationToken);
            await dockerPool.ReturnContainerAsync(containerId, cancellationToken);
        }
        catch (Exception ex)
        {
            // Handle exceptions (e.g., log them)
            Console.WriteLine($"Error submitting code: {ex.Message}");
        }
        finally
        {
            
        }
    }

    private async Task JudgeCode(SubmissionRequest submissionRequest, string containerId,
        CancellationToken cancellationToken)
    {
        var execCmd = compilerCommand.CreateCompileCommand(submissionRequest);
        await dockerPool.ExecCmdFromContainer(containerId, execCmd, cancellationToken);
    }

    private async Task CreateFile(SubmissionRequest submissionRequest, string containerId,
        CancellationToken cancellationToken)
    {
        var extension = Constant.GetLanguageExtension(submissionRequest.Language);
        var cmdCreateFile =
            fileCommand.CreateSourceFile(submissionRequest.Source, submissionRequest.Id, containerId, extension);

        await dockerPool.ExecCmdFromContainer(containerId, cmdCreateFile, cancellationToken);
    }
}