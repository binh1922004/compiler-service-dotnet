using CompilerService.Models;
using CompilerService.Services;

namespace CompilerService.Infrastructure.Kafka.Handlers;

public class SubmissionHandler(
    ICompileService compileService,
    IKafkaClient kafkaClient,
    ILogger<SubmissionHandler> logger) : IMessageHandler<SubmissionRequest>
{
    private const string ResultTopic = "result-topic";

    public async Task HandleAsync(SubmissionRequest message, CancellationToken cancellationToken)
    {
        logger.LogInformation("Processing submission {SubmissionId} for problem {ProblemId}",
            message.Id, message.Problem.Id);

        try
        {
            var result = await compileService.SubmitCode(message, cancellationToken);
            if (result != null)
            {
                await kafkaClient.ProduceAsync(ResultTopic, message.Id, result, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process submission {SubmissionId}", message.Id);

            var errorResponse = new SubmissionResponse
            {
                Id = message.Id,
                Status = SubmissionStatus.IE,
            };
            await kafkaClient.ProduceAsync(ResultTopic, message.Id, errorResponse, cancellationToken);
        }
    }
}
