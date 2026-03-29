using Confluent.Kafka;

namespace CompilerService.Infrastructure.Kafka;

/// <summary>
/// Centralized Kafka client that handles both consuming and producing messages.
/// </summary>
public interface IKafkaClient : IDisposable
{
    /// <summary>
    /// Subscribe to a Kafka topic and start consuming messages.
    /// </summary>
    void Subscribe(string topic);
    
    /// <summary>
    /// Consume a single message from the subscribed topic.
    /// Returns null if no message is available.
    /// </summary>
    ConsumeResult<string, string> Consume(CancellationToken cancellationToken);
    
    /// <summary>
    /// Produce a message to a specific Kafka topic.
    /// </summary>
    Task ProduceAsync<T>(string topic, string key, T message, CancellationToken cancellationToken = default);
}
