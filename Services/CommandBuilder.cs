using CompilerService.Configuration;
using CompilerService.Models;
using Microsoft.Extensions.Options;

namespace CompilerService.Services;

public class CommandBuilder(IOptions<WorkSettings> workSettings)
{
    private readonly WorkSettings _workSettings = workSettings.Value;
    
    public string CreateCompileCommand(SubmissionRequest submissionRequest)
    {
        var extension = Constants.GetLanguageExtension(submissionRequest.Language);
        var submissionDir = $"{_workSettings.SubmissionDir}/{submissionRequest.Id}";
        var filePath = $"{submissionDir}/{submissionRequest.Id}.{extension}";
        var command = $"python {_workSettings.ScriptDir}/judge.py {filePath} {submissionRequest.Problem.Id} {submissionRequest.Problem.Time} {submissionRequest.Problem.Memory} --icpc";
        return command;
    }
    
    public string CreateSourceFileCommand(string sourceCode, string submissionId, string extension)
    {
        var submissionDir = $"{_workSettings.SubmissionDir}/{submissionId}";
        var filePath = $"{submissionDir}/{submissionId}.{extension}";
        return $"mkdir -p {submissionDir} && cat > {filePath} << 'EOF'\n{sourceCode}\nEOF\n";
    }

    public string CreateDeleteSourceFileCommand(string submissionRequestId, string extension)
    {
        var submissionDir = $"{_workSettings.SubmissionDir}/{submissionRequestId}";
        var filePath = $"{submissionDir}/{submissionRequestId}.{extension}";
        return $"rm -f {filePath}";
    }
}
