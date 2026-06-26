namespace CompilerService.Configuration;

public class KafkaSettings
{
    public string SubmissionTopic { get; set; } = "submission";
    public string ResultTopic { get; set; } = "result";

    public string TestCaseGenerationRequestTopic { get; set; } = "test-case-generation-request";
    public string TestCaseGenerationResponseTopic { get; set; } = "test-case-generation-result";

    public string PreTestRequestTopic { get; set; } = "compiler.pre-test.request";
    public string PreTestResultTopic { get; set; } = "compiler.pre-test.response";
}