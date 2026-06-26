using CompilerService.Configuration;
using CompilerService.Models;
using CompilerService.Services;
using Microsoft.Extensions.Options;

namespace CompilerService.Infrastructure.Kafka.Handlers;

public class PreTestHandler(
    ICompileService compileService,
    IKafkaClient kafkaClient,
    IOptions<KafkaSettings> kafkaSettings,
    ILogger<PreTestHandler> logger) : IMessageHandler<PreTestRequest>
{
    private readonly KafkaSettings _kafkaSettings = kafkaSettings.Value;

    public async Task HandleAsync(PreTestRequest message, CancellationToken cancellationToken)
    {
        logger.LogInformation("Processing pre-test {PreTestId} for user {UserId}", message.Id, message.UserId);

        try
        {
            var result = await compileService.RunPreTest(message, cancellationToken);
            await kafkaClient.ProduceAsync(_kafkaSettings.PreTestResultTopic, message.Id, result!, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Infrastructure failure processing pre-test {PreTestId}", message.Id);

            var errorResponse = new PreTestResponse
            {
                Id = message.Id,
                Status = "IE",
                Error = ex.Message
            };
            await kafkaClient.ProduceAsync(_kafkaSettings.PreTestResultTopic, message.Id, errorResponse, cancellationToken);
        }
    }
}
