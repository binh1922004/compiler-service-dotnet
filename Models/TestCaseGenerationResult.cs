namespace CompilerService.Models;

public class TestCaseGenerationResult
{
    public string PlanId { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string S3Key { get; set; } = string.Empty;
    public int Version { get; set; }
    public int TestCount { get; set; }
    public string? Error { get; set; }
}