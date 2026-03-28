using Confluent.Kafka;

namespace CompilerService.Kafka;

public class KafkaDependentProducer<TK, TV>(KafkaClientHandle handle)
{
    private readonly IProducer<TK, TV> _kafkaProducer = new DependentProducerBuilder<TK, TV>(handle.Handle).Build();


    public Task ProduceAsync(string topic, Message<TK, TV> message)
    {
        return _kafkaProducer.ProduceAsync(topic, message);
    }
    
    /// <summary>
    ///     Asynchronously produce a message and expose delivery information
    ///     via the provided callback function. Use this method of producing
    ///     if you would like flow of execution to continue immediately, and
    ///     handle delivery information out-of-band.
    /// </summary>
    public void Produce(string topic, Message<TK, TV> message, Action<DeliveryReport<TK, TV>> deliveryHandler = null)
        => this._kafkaProducer.Produce(topic, message, deliveryHandler);

    public void Flush(TimeSpan timeout)
        => this._kafkaProducer.Flush(timeout);
}