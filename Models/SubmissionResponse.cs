namespace CompilerService.Models;

public class SubmissionResponse
{
    public string Id { get; set; }
    public string problemId { get; set; }
    public string Source { get; set; }
    public Language Language { get; set; }
    public string Status { get; set; }
    public double Time { get; set; }
    public double Memory { get; set; }
    public int Passed { get; set; }
    public int Total { get; set; }
}
