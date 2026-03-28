using CompilerService.DTO;

namespace CompilerService.Services;

public interface ICompileService
{
    Task SubmitCode(SubmissionRequest submissionRequest, CancellationToken cancellationToken);
}