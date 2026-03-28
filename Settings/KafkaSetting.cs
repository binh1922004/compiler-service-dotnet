namespace CompilerService.Settings;

public class KafkaSetting
{
    public string BootstrapServers { get; set; } = "localhost:9092";
    public string Topic { get; set; } = "submission";
}