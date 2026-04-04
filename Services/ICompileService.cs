using CompilerService.Models;

namespace CompilerService.Services;

public interface ICompileService
{
    Task<SubmissionResponse?> SubmitCode(SubmissionRequest submissionRequest, CancellationToken cancellationToken);
}