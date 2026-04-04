using System.Text.Json.Serialization;

namespace CompilerService.Models;

public class SubmissionResponse
{
    [JsonPropertyName("submissionId")]
    public string Id { get; set; }
    
    [JsonPropertyName("status")]
    public string Status { get; set; }
    
    [JsonPropertyName("passed")]
    public int Passed { get; set; }
    
    [JsonPropertyName("total")]
    public int Total { get; set; }
    
    [JsonPropertyName("max_time")]
    public double Time { get; set; }
    
    [JsonPropertyName("max_memory_mb")]
    public double Memory { get; set; }
}
