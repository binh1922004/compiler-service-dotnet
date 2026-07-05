using System.Text.Json.Serialization;

namespace CompilerService.Models;

public class Problem
{
    [JsonPropertyName("_id")]
    public string Id { get; set; }
    public int Time { get; set; }
    public int Memory { get; set; }
    public int Version { get; set; }
    public int NumberOfTestCases { get; set; }
    [JsonPropertyName("testCaseS3Key")]
    public string? TestCaseS3Key { get; set; }
}