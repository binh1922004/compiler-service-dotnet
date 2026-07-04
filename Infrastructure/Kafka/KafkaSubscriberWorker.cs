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
    IServiceScopeFactory serviceScopeFactory,
    IConfiguration configuration,
    ILogger<KafkaSubscriberWorker> logger,
    IOptions<KafkaSettings> kafkaSettings)
    : BackgroundService
{
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };
    private readonly SemaphoreSlim _concurrencyLimit = new(configuration.GetValue<int>(Constants.NumberOfWorkersSetting));

    private readonly KafkaSettings _kafkaSettings = kafkaSettings.Value;
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return Task.Run(() => SubscribeLoop(stoppingToken), stoppingToken);
    }

    private async Task SubscribeLoop(CancellationToken stoppingToken)
    {
        string[] topics =
        [
            _kafkaSettings.SubmissionTopic,
            _kafkaSettings.TestCaseGenerationRequestTopic,
            _kafkaSettings.PreTestRequestTopic
        ];
        kafkaClient.Subscribe(topics);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var consumeResult = kafkaClient.Consume(stoppingToken);
                var topic = consumeResult?.Topic;
                var json = consumeResult?.Message?.Value;
                if (json == null || topic == null) continue;

                if (topic == _kafkaSettings.SubmissionTopic)
                {
                    var message = JsonSerializer.Deserialize<SubmissionRequest>(json, _jsonOptions);
                    if (message == null) continue;
                    
                    // Wait until we have a free slot
                    await _concurrencyLimit.WaitAsync(stoppingToken);

                    _ = Task.Run(async () =>
                    {
                        // 1. CREATE A BRAND NEW SCOPE FOR THIS SPECIFIC TASK
                        using var scope = serviceScopeFactory.CreateScope();
                        
                        // 2. RESOLVE A FRESH HANDLER FROM THE SCOPE
                        var handler = scope.ServiceProvider.GetRequiredService<IMessageHandler<SubmissionRequest>>();

                        try
                        {
                            // 3. EXECUTE SAFELY
                            await handler.HandleAsync(message, stoppingToken);
                        }
                        catch (Exception ex)
                        {
                            logger.LogError(ex, "Error handling submission");
                        }
                        finally
                        {
                            _concurrencyLimit.Release();
                        }
                    }, stoppingToken);
                }
                else if (topic == _kafkaSettings.TestCaseGenerationRequestTopic)
                {
                    var message = JsonSerializer.Deserialize<TestCasePlan>(json, _jsonOptions);
                    if (message == null) continue;

                    // Note: If test cases take a long time to generate, you should 
                    // apply the exact same Semaphore/Task.Run/Scope pattern here!
                    using var scope = serviceScopeFactory.CreateScope();
                    var handler = scope.ServiceProvider.GetRequiredService<IMessageHandler<TestCasePlan>>();
                    await handler.HandleAsync(message, stoppingToken);
                }
                else if (topic == _kafkaSettings.PreTestRequestTopic)
                {
                    var message = JsonSerializer.Deserialize<PreTestRequest>(json, _jsonOptions);
                    if (message == null) continue;

                    using var scope = serviceScopeFactory.CreateScope();
                    var handler = scope.ServiceProvider.GetRequiredService<IMessageHandler<PreTestRequest>>();
                    await handler.HandleAsync(message, stoppingToken);
                }
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in Kafka subscriber loop");
            }
        }
    }
}