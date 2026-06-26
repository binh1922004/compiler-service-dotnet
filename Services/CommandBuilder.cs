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

    /// <summary>
    /// Builds a Docker exec command that invokes pre_test_judge.py with the user's source file
    /// and the input/expected strings passed as base64-encoded arguments.
    ///
    /// Base64 encoding avoids all shell-quoting problems (newlines, quotes, backslashes, etc.)
    /// without creating any extra files on disk.
    /// </summary>
    public string CreatePreTestCommand(PreTestRequest request)
    {
        var extension = Constants.GetLanguageExtension(request.Language);
        var submissionDir = $"{_workSettings.SubmissionDir}/{request.Id}";
        var filePath = $"{submissionDir}/{request.Id}.{extension}";

        // Base64-encode the input and expected output so they are safe to pass as bare shell args
        var inputB64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(request.Input));
        var expectedB64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(request.ExpectedOutput));

        return $"python3 {_workSettings.ScriptDir}/pre_test_judge.py {filePath} " +
               $"--input {inputB64} --expected {expectedB64} " +
               $"{request.TimeLimit} {request.MemoryLimit}";
    }

    /// <summary>
    /// Creates the submission directory and writes only the source file.
    /// No input/expected files are created — those values are passed inline to the script.
    /// </summary>
    public string CreatePreTestSourceFileCommand(PreTestRequest request)
    {
        var extension = Constants.GetLanguageExtension(request.Language);
        var submissionDir = $"{_workSettings.SubmissionDir}/{request.Id}";
        var filePath = $"{submissionDir}/{request.Id}.{extension}";

        return $"mkdir -p {submissionDir} && cat > {filePath} << 'SRCEOF'\n{request.Source}\nSRCEOF\n";
    }
}