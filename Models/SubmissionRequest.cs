using System.Text.Json.Serialization;

namespace CompilerService.Models;

public class SubmissionRequest
{
    [JsonPropertyName("_id")]
    public string Id { get; set; }
    [JsonPropertyName("problem")]
    public Problem Problem { get; set; }
    public string Source { get; set; }
    public Language Language { get; set; }
}
