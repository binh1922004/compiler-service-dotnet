using System.Text.Json.Serialization;

namespace CompilerService.Models;

public class PreTestRequest
{
    [JsonPropertyName("_id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("userId")]
    public string UserId { get; set; } = string.Empty;

    public string Source { get; set; } = string.Empty;

    public Language Language { get; set; }

    public string Input { get; set; } = string.Empty;

    public string ExpectedOutput { get; set; } = string.Empty;

    /// <summary>Time limit in seconds.</summary>
    public int TimeLimit { get; set; } = 5;

    /// <summary>Memory limit in MB.</summary>
    public int MemoryLimit { get; set; } = 256;
}
