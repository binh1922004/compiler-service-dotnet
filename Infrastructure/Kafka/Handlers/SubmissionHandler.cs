using CompilerService.Models;
using CompilerService.Services;

namespace CompilerService.Infrastructure.Kafka.Handlers;

public class SubmissionHandler(
    ICompileService compileService,
    IKafkaClient kafkaClient,
    ILogger<SubmissionHandler> logger) : IMessageHandler<SubmissionRequest>
{
    private const string ResultTopic = "submission-result-topic";

    public async Task HandleAsync(SubmissionRequest message, CancellationToken cancellationToken)
    {
        logger.LogInformation("Processing submission {SubmissionId} for problem {ProblemId}",
            message.Id, message.Problem.Id);

        try
        {
            await compileService.SubmitCode(message, cancellationToken);

            // TODO: When CompileService returns a result, publish it:
            // await kafkaClient.ProduceAsync(ResultTopic, message.Id, result, cancellationToken);

            logger.LogInformation("Completed submission {SubmissionId}", message.Id);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process submission {SubmissionId}", message.Id);

            var errorResponse = new SubmissionResponse
            {
                Id = message.Id,
                problemId = message.Problem.Id,
                Status = SubmissionStatus.IE,
                Source = message.Source,
                Language = message.Language
            };
            await kafkaClient.ProduceAsync(ResultTopic, message.Id, errorResponse, cancellationToken);
        }
    }
}
