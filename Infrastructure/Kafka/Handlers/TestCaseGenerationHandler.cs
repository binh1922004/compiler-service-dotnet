using CompilerService.Configuration;
using CompilerService.Models;
using CompilerService.Services;
using Microsoft.Extensions.Options;

namespace CompilerService.Infrastructure.Kafka.Handlers;

public class TestCaseGenerationHandler(
    ICompileService compileService,
    IKafkaClient kafkaClient,
    IOptions<KafkaSettings> kafkaSettings,
    ILogger<TestCaseGenerationHandler> logger) : IMessageHandler<TestCasePlan>
{
    private readonly KafkaSettings _kafkaSettings = kafkaSettings.Value;

    public async Task HandleAsync(TestCasePlan message, CancellationToken cancellationToken)
    {
        logger.LogInformation("Processing test case generation for PlanId={PlanId}", message.PlanId);

        try
        {
            var result = await compileService.GenerateTestCases(message, cancellationToken);
            await kafkaClient.ProduceAsync(
                _kafkaSettings.TestCaseGenerationResponseTopic,
                message.PlanId,
                result,
                cancellationToken);

            logger.LogInformation(
                "Test case generation completed for PlanId={PlanId}. Success={Success}, TestCount={TestCount}",
                message.PlanId, result.Success, result.TestCount);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process test case generation for PlanId={PlanId}", message.PlanId);

            var errorResult = new TestCaseGenerationResult
            {
                PlanId = message.PlanId,
                Success = false,
                Error = ex.Message
            };
            await kafkaClient.ProduceAsync(
                _kafkaSettings.TestCaseGenerationResponseTopic,
                message.PlanId,
                errorResult,
                cancellationToken);
        }
    }
}