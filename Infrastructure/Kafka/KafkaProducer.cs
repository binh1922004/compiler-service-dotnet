using System.Text.Json;
using System.Text.Json.Serialization;
using CompilerService.Configuration;
using Confluent.Kafka;

namespace CompilerService.Infrastructure.Kafka;

public class KafkaProducer : IKafkaProducer, IDisposable
{
    private readonly IProducer<string, string> _producer;
    private readonly ILogger<KafkaProducer> _logger;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    public KafkaProducer(IConfiguration configuration, ILogger<KafkaProducer> logger)
    {
        _logger = logger;
        var producerConfig = new ProducerConfig();
        configuration.GetSection(Constants.KafkaProducerSettings).Bind(producerConfig);
        _producer = new ProducerBuilder<string, string>(producerConfig).Build();
    }

    public async Task ProduceAsync<T>(string topic, string key, T message, CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(message, _jsonOptions);
        var kafkaMessage = new Message<string, string>
        {
            Key = key,
            Value = json
        };

        var result = await _producer.ProduceAsync(topic, kafkaMessage, cancellationToken);
        _logger.LogDebug("Produced message to {Topic} [{Partition}] @ offset {Offset}",
            result.Topic, result.Partition.Value, result.Offset.Value);
    }

    public void Dispose()
    {
        _producer.Flush(TimeSpan.FromSeconds(5));
        _producer.Dispose();
    }
}
