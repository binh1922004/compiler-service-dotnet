using CompilerService.Settings;
using Microsoft.Extensions.Options;

namespace CompilerService.Commands;

public class FileCommand(IOptions<WorkSetting> workSetting)
{
    private readonly WorkSetting _workSetting = workSetting.Value;
    public string CreateSourceFile(string sourceCode, string submissionId, string containerId, string extension)
    {
        var submissionDir = $"{_workSetting.SubmissionDir}/{submissionId}";
        var filePath = $"{submissionDir}/{submissionId}.{extension}";
        return $"mkdir -p {submissionDir} && cat > {filePath} << EOF\n{sourceCode}\nEOF\n";
    }
}