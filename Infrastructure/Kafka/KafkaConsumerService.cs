using System.Text.Json;
using System.Text.Json.Serialization;
using CompilerService.Configuration;
using CompilerService.Models;
using CompilerService.Services;
using Confluent.Kafka;
using Microsoft.Extensions.Options;

namespace CompilerService.Infrastructure.Kafka;

public class KafkaConsumerService : BackgroundService
{
    private readonly IConsumer<string, string> _kafkaConsumer;
    private readonly ICompileService _compileService;
    private readonly ILogger<KafkaConsumerService> _logger;
    private readonly JsonSerializerOptions _jsonSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };
    
    
    public KafkaConsumerService(IConfiguration configuration, ICompileService compileService, ILogger<KafkaConsumerService> logger)
    {
        _compileService = compileService;
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
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var cr = _kafkaConsumer.Consume(cancellationToken: stoppingToken);
                
                var jsonString = cr.Message.Value;
                var submissionRequest = JsonSerializer.Deserialize<SubmissionRequest>(jsonString, _jsonSerializerOptions);
                
                if (submissionRequest == null)
                {
                    _logger.LogWarning("Failed to deserialize message");
                    continue;
                }
                _logger.LogInformation("Thread {ThreadId} received submission {SubmissionId} for problem {ProblemId}",
                    Environment.CurrentManagedThreadId, submissionRequest.Id, submissionRequest.Problem.Id);
                await _compileService.SubmitCode(submissionRequest, stoppingToken);
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
