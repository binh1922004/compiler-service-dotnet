using CompilerService.Utilities;
using Confluent.Kafka;

namespace CompilerService.Kafka;

public class KafkaClientHandle : IDisposable
{
    private readonly IProducer<byte[], byte[]> _kafkaProducer;

    public KafkaClientHandle(IConfiguration configuration)
    {
        var conf = new ProducerConfig();
        configuration.GetSection(Constant.KafkaProducerSettings).Bind(conf);
        _kafkaProducer = new ProducerBuilder<byte[], byte[]>(conf).Build();
    }
    
    public Handle Handle => _kafkaProducer.Handle;

    public void Dispose()
    {
        _kafkaProducer.Flush();
        _kafkaProducer.Dispose();
    }
}