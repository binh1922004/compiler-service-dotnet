namespace CompilerService.Configuration;

public class KafkaSettings
{
    public string SubmissionTopic { get; set; } = "submission";
    public string ResultTopic { get; set; } = "result";
}
