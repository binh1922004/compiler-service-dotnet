using System.Text.Json.Serialization;

namespace CompilerService.Models;

public class TestCasePreview
{
    [JsonPropertyName("index")]
    public int Index { get; set; }

    [JsonPropertyName("inputPreview")]
    public string InputPreview { get; set; } = string.Empty;

    [JsonPropertyName("outputPreview")]
    public string OutputPreview { get; set; } = string.Empty;
}
