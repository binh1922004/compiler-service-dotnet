using CompilerService.Models;

namespace CompilerService.Services;

public interface ICompileService
{
    Task SubmitCode(SubmissionRequest submissionRequest, CancellationToken cancellationToken);
}