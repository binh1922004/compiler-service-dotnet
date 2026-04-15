using CompilerService.Models;

namespace CompilerService.Configuration;

public static class Constants
{
    public static string GetLanguageExtension(Language language)
    {
        return language switch
        {
            Language.cpp => "cpp",
            Language.py => "py",
            Language.js => "js",
            Language.pl => "pl",
            Language.rb => "rb",
            _ => throw new ArgumentOutOfRangeException(nameof(language), language, null)
        };
    }

    public const string WorkDirSetting = "WorkdirConfig";
    public const string KafkaSetting = "Kafka:Topics";

    public const string KafkaProducerSettings = "Kafka:ProducerSettings";
    public const string KafkaConsumerSettings = "Kafka:ConsumerSettings";
    public const string AwsS3Setting = "AWS:S3";
}
