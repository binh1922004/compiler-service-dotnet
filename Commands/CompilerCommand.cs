using CompilerService.DTO;
using CompilerService.Settings;
using CompilerService.Utilities;
using Microsoft.Extensions.Options;

namespace CompilerService.Commands;

public class CompilerCommand(IOptions<WorkSetting> workSetting)
{
    private readonly WorkSetting _workSetting = workSetting.Value;
    public string CreateCompileCommand(SubmissionRequest submissionRequest)
    {
        var extension = Constant.GetLanguageExtension(submissionRequest.Language);
        var submissionDir = $"{_workSetting.SubmissionDir}/{submissionRequest.Id}";
        var filePath = $"{submissionDir}/{submissionRequest.Id}.{extension}";
        var command = $"python {_workSetting.ScriptDir}/judge.py {filePath} {submissionRequest.Problem.Id} {submissionRequest.Problem.Time} {submissionRequest.Problem.Memory} --icpc";
        return command;
    }
}