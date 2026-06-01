using System.Text.Json.Serialization;

namespace CompilerService.Models;

public class TestCaseScriptOutput
{
    [JsonPropertyName("status")] public string Status { get; set; } = string.Empty;

    [JsonPropertyName("zipPath")] public string? ZipPath { get; set; }

    [JsonPropertyName("testCount")] public int TestCount { get; set; }

    [JsonPropertyName("error")] public string? Error { get; set; }

    public bool IsSuccess => Status == "success";
}