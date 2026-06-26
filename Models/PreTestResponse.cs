using System.Text.Json.Serialization;

namespace CompilerService.Models;

public class PreTestResponse
{
    public string Id { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;
    [JsonPropertyName("actualOutput")]

    public string? ActualOutput { get; set; }

    /// <summary>Execution time in seconds.</summary>
    [JsonPropertyName("time")]
    public double Time { get; set; }

    /// <summary>Peak memory usage in MB.</summary>
    [JsonPropertyName("memoryMb")]
    public double Memory { get; set; }

    public string? Error { get; set; }
}
