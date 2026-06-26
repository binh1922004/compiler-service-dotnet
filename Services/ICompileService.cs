using CompilerService.Models;

namespace CompilerService.Services;

public interface ICompileService
{
    Task<SubmissionResponse?> SubmitCode(SubmissionRequest submissionRequest, CancellationToken cancellationToken);

    Task<TestCaseGenerationResult> GenerateTestCases(TestCasePlan plan, CancellationToken cancellationToken);

    Task<PreTestResponse?> RunPreTest(PreTestRequest request, CancellationToken cancellationToken);
}