using System.Text.Json;
using System.Text.Json.Serialization;
using CompilerService.Configuration;
using Confluent.Kafka;

namespace CompilerService.Infrastructure.Kafka;

/// <summary>
/// Centralized Kafka client that wraps both consumer and producer.
/// All Kafka communication goes through this single class.
/// </summary>
public class KafkaClient : IKafkaClient
{
    private readonly IConsumer<string, string> _consumer;
    private readonly IProducer<string, string> _producer;
    private readonly ILogger<KafkaClient> _logger;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    public KafkaClient(IConfiguration configuration, ILogger<KafkaClient> logger)
    {
        _logger = logger;

        // Consumer setup
        var consumerConfig = new ConsumerConfig();
        configuration.GetSection(Constants.KafkaConsumerSettings).Bind(consumerConfig);
        _consumer = new ConsumerBuilder<string, string>(consumerConfig).Build();

        // Producer setup
        var producerConfig = new ProducerConfig();
        configuration.GetSection(Constants.KafkaProducerSettings).Bind(producerConfig);
        _producer = new ProducerBuilder<string, string>(producerConfig).Build();

        _logger.LogInformation("KafkaClient initialized");
    }

    public void Subscribe(string topic)
    {
        _consumer.Subscribe(topic);
        _logger.LogInformation("Subscribed to topic '{Topic}'", topic);
    }

    public ConsumeResult<string, string> Consume(CancellationToken cancellationToken)
    {
        var result = _consumer.Consume(cancellationToken);
        return result;
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
        _logger.LogDebug("Produced to {Topic} [{Partition}] @ {Offset}",
            result.Topic, result.Partition.Value, result.Offset.Value);
    }

    public void Dispose()
    {
        _consumer.Close();
        _consumer.Dispose();
        _producer.Flush(TimeSpan.FromSeconds(5));
        _producer.Dispose();
        _logger.LogInformation("KafkaClient disposed");
    }
}
