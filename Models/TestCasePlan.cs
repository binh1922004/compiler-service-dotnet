namespace CompilerService.Models;

public class TestCasePlan
{
    public string PlanId { get; set; } = string.Empty;

    public int Version { get; set; } = 1;

    // Python code to generate input and output for test cases
    public string InputCode { get; set; } = string.Empty;
    public string OutPutCode { get; set; } = string.Empty;
}