namespace CompilerService.Utilities;

public class Constant
{
    public static string GetLanguageExtension(Language language)
    {
        return language switch
        {
            Language.cpp => "cpp",
            Language.py => "py",
            Language.js => "js",
            _ => throw new ArgumentOutOfRangeException(nameof(language), language, null)
        };
    }

    public const string WorkDirSetting = "WorkdirConfig";
    public const string KafkaSetting = "KafkaConfig";

    public const string KafkaProducerSettings = "Kafka:ProducerSettings";
    public const string KafkaConsumerSettings = "Kafka:ConsumerSettings";
    public const string AwsS3Setting = "AWS:S3";
}