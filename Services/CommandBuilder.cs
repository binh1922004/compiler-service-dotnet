using CompilerService.Configuration;
using CompilerService.Models;
using Microsoft.Extensions.Options;

namespace CompilerService.Services;

public class CommandBuilder(IOptions<WorkSettings> workSettings)
{
    private const string GenCmd = $"python3 {FileConstant.InputPythonFileName}";
    private const string SolCmd = $"python3 {FileConstant.OutputPythonFileName}";
    private const int TimeOut = 60;
    private readonly WorkSettings _workSettings = workSettings.Value;

    public string CreateCompileCommand(SubmissionRequest submissionRequest)
    {
        var extension = Constants.GetLanguageExtension(submissionRequest.Language);
        var submissionDir = $"{_workSettings.SubmissionDir}/{submissionRequest.Id}";
        var filePath = $"{submissionDir}/{submissionRequest.Id}.{extension}";
        var problemVersion = submissionRequest.Problem.Version == 0 ? "" : $"-v{submissionRequest.Problem.Version}";
        var problemPath = $"{submissionRequest.Problem.Id}{problemVersion}";
        var command =
            $"python {_workSettings.ScriptDir}/judge.py {filePath} {problemPath} {submissionRequest.Problem.Time} {submissionRequest.Problem.Memory} --icpc";
        return command;
    }

    public string CreateSourceFileCommand(string sourceCode, string submissionId, string extension)
    {
        var submissionDir = $"{_workSettings.SubmissionDir}/{submissionId}";
        var filePath = $"{submissionDir}/{submissionId}.{extension}";
        return $"mkdir -p {submissionDir} && cat > {filePath} << 'EOF'\n{sourceCode}\nEOF\n";
    }

    public string CreateDeleteSubmissionFolderCommand(string submissionRequestId)
    {
        var submissionDir = $"{_workSettings.SubmissionDir}/{submissionRequestId}";
        return $"rm -rf {submissionDir}";
    }

    public string GenerateTestCaseCommand(string planName)
    {
        // Using string interpolation for the path is fine for Linux containers,
        // though Path.Combine(_workSettings.TestCaseDir, planName) is the safest .NET habit.
        var outDir = $"{_workSettings.TestCaseDir}/{planName}";

        // Fix: Added escaped quotes (\") around arguments with spaces, and added a space before {timeOut}
        return
            $"python3 {_workSettings.ScriptDir}/test_case_generator.py --gen \"{GenCmd}\" --sol \"{SolCmd}\" --outdir \"{outDir}\" --timeout {TimeOut}";
    }

    public string CreateTestCaseFilesCommand(string planName, string inputCode, string outputCode)
    {
        var planDir = $"{_workSettings.TestCaseDir}/{planName}";
        var inputPath = $"{planDir}/{FileConstant.InputPythonFileName}";
        var outputPath = $"{planDir}/{FileConstant.OutputPythonFileName}";

        // Create directory, write input.py and output.py using heredoc
        return
            $"mkdir -p {planDir} && cat > {inputPath} << 'INPUTEOF'\n{inputCode}\nINPUTEOF\ncat > {outputPath} << 'OUTPUTEOF'\n{outputCode}\nOUTPUTEOF\n";
    }

    public string DeleteTestCaseFolderCommand(string planId)
    {
        var planDir = $"{_workSettings.TestCaseDir}/{planId}";
        return $"rm -rf {planDir}";
    }
}