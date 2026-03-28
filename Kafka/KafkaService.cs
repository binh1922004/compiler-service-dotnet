using System.Text.Json;
using System.Text.Json.Serialization;
using CompilerService.DTO;
using CompilerService.Services;
using CompilerService.Settings;
using CompilerService.Utilities;
using Confluent.Kafka;
using Microsoft.Extensions.Options;

namespace CompilerService.Kafka;

public class KafkaService : BackgroundService
{
    private readonly IConsumer<string, string> _kafkaConsumer;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ICompileService _compileService;
    private readonly ILogger<KafkaService> _logger;
    private readonly JsonSerializerOptions _jsonSerializerOptions = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };
    
    
    public KafkaService(IConfiguration configuration, IServiceScopeFactory serviceScopeFactory, ICompileService compileService, ILogger<KafkaService> logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _compileService = compileService;
        _logger = logger;
        var consumerConfig = new ConsumerConfig();
        configuration.GetSection(Constant.KafkaConsumerSettings).Bind(consumerConfig);
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
                    Console.WriteLine("Failed to deserialize message.");
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
                // Consumer errors should generally be ignored (or logged) unless fatal.
                Console.WriteLine($"Consume error: {e.Error.Reason}");

                if (e.Error.IsFatal)
                {
                    break;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Unexpected error: {e}");
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