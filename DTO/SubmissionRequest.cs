using System.Text.Json.Serialization;
using CompilerService.Models;
using CompilerService.Utilities;

namespace CompilerService.DTO;

public class SubmissionRequest
{
    [JsonPropertyName("_id")]
    public string Id { get; set; }
    [JsonPropertyName("problem")]
    public Problem Problem { get; set; }
    public string Source { get; set; }
    public Language Language { get; set; }
}