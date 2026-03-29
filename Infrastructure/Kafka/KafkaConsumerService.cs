using System.Text.Json;
using System.Text.Json.Serialization;
using CompilerService.Configuration;
using CompilerService.Models;
using Confluent.Kafka;

namespace CompilerService.Infrastructure.Kafka;

public class KafkaConsumerService : BackgroundService
{
    private readonly IConsumer<string, string> _kafkaConsumer;
    private readonly IMessageHandler<SubmissionRequest> _submissionHandler;
    private readonly ILogger<KafkaConsumerService> _logger;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };


    public KafkaConsumerService(
        IConfiguration configuration,
        IMessageHandler<SubmissionRequest> submissionHandler,
        ILogger<KafkaConsumerService> logger)
    {
        _submissionHandler = submissionHandler;
        _logger = logger;
        var consumerConfig = new ConsumerConfig();
        configuration.GetSection(Constants.KafkaConsumerSettings).Bind(consumerConfig);
        _kafkaConsumer = new ConsumerBuilder<string, string>(consumerConfig).Build();
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return Task.Run(() => StartConsumerLoop(stoppingToken), stoppingToken);
    }

    private async Task StartConsumerLoop(CancellationToken stoppingToken)
    {
        _kafkaConsumer.Subscribe("submission-topic");
        _logger.LogInformation("Kafka consumer started, listening on 'submission-topic'");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var cr = _kafkaConsumer.Consume(cancellationToken: stoppingToken);

                var jsonString = cr.Message.Value;
                var submissionRequest = JsonSerializer.Deserialize<SubmissionRequest>(jsonString, _jsonOptions);

                if (submissionRequest == null)
                {
                    _logger.LogWarning("Failed to deserialize message");
                    continue;
                }

                await _submissionHandler.HandleAsync(submissionRequest, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (ConsumeException e)
            {
                _logger.LogError(e, "Consume error: {Reason}", e.Error.Reason);

                if (e.Error.IsFatal)
                {
                    break;
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Unexpected error in Kafka consumer loop");
                break;
            }
        }
    }

    public override void Dispose()
    {
        _kafkaConsumer.Close();
        _kafkaConsumer.Dispose();
        base.Dispose();
    }
}
