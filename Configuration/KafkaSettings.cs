namespace CompilerService.Configuration;

public class KafkaSettings
{
    public string SubmissionTopic { get; set; } = "submission";
    public string ResultTopic { get; set; } = "result";

    public string TestCaseGenerationRequestTopic { get; set; } = "test-case-generation-request";
    public string TestCaseGenerationResponseTopic { get; set; } = "test-case-generation-result";
}