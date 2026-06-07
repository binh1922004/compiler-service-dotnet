using System.Text.Json;
using System.Text.Json.Serialization;
using CompilerService.Configuration;
using CompilerService.Models;
using Microsoft.Extensions.Options;

namespace CompilerService.Infrastructure.Kafka;

/// <summary>
///     Thin BackgroundService — only subscribes to Kafka and dispatches messages to handlers.
///     Contains zero business logic.
/// </summary>
public class KafkaSubscriberWorker(
    IKafkaClient kafkaClient,
    IMessageHandler<SubmissionRequest> submissionHandler,
    IMessageHandler<TestCasePlan> testCaseGenerationHandler,
    ILogger<KafkaSubscriberWorker> logger,
    IOptions<KafkaSettings> kafkaSettings)
    : BackgroundService
{
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly KafkaSettings _kafkaSettings = kafkaSettings.Value;
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return Task.Run(() => SubscribeLoop(stoppingToken), stoppingToken);
    }

    private async Task SubscribeLoop(CancellationToken stoppingToken)
    {
        string[] topics = [_kafkaSettings.SubmissionTopic, _kafkaSettings.TestCaseGenerationRequestTopic];
        kafkaClient.Subscribe(topics);
        while (!stoppingToken.IsCancellationRequested)
            try
            {
                var consumeResult = kafkaClient.Consume(stoppingToken);
                var topic = consumeResult?.Topic;
                var json = consumeResult?.Message?.Value;
                if (json == null || topic == null) continue;

                if (topic == _kafkaSettings.SubmissionTopic)
                {
                    var message = JsonSerializer.Deserialize<SubmissionRequest>(json, _jsonOptions);
                    if (message == null)
                    {
                        logger.LogWarning("Failed to deserialize submission message");
                        continue;
                    }

                    // Dispatch to handler — all business logic lives there
                    await submissionHandler.HandleAsync(message, stoppingToken);
                }
                else if (topic == _kafkaSettings.TestCaseGenerationRequestTopic)
                {
                    var message = JsonSerializer.Deserialize<TestCasePlan>(json, _jsonOptions);
                    if (message == null)
                    {
                        logger.LogWarning("Failed to deserialize test case generation message");
                        continue;
                    }

                    await testCaseGenerationHandler.HandleAsync(message, stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in Kafka subscriber loop");
            }
    }
}