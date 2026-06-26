using CompilerService.Configuration;
using CompilerService.Models;
using CompilerService.Services;
using Microsoft.Extensions.Options;

namespace CompilerService.Infrastructure.Kafka.Handlers;

public class SubmissionHandler(
    ICompileService compileService,
    IKafkaClient kafkaClient,
    IOptions<KafkaSettings> kafkaSettings,
    ILogger<SubmissionHandler> logger) : IMessageHandler<SubmissionRequest>
{
    private readonly KafkaSettings _kafkaSettings = kafkaSettings.Value;

    public async Task HandleAsync(SubmissionRequest message, CancellationToken cancellationToken)
    {
        logger.LogInformation("Processing submission {SubmissionId} for problem {ProblemId}",
            message.Id, message.Problem.Id);

        try
        {
            var result = await compileService.SubmitCode(message, cancellationToken);
            await kafkaClient.ProduceAsync(_kafkaSettings.ResultTopic, message.Id, result!, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Infrastructure failure processing submission {SubmissionId}", message.Id);

            var errorResponse = new SubmissionResponse
            {
                Id = message.Id,
                Status = SubmissionStatus.IE
            };
            await kafkaClient.ProduceAsync(_kafkaSettings.ResultTopic, message.Id, errorResponse, cancellationToken);
        }
    }
}