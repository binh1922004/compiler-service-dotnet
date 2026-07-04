using CompilerService.Configuration;
using CompilerService.Infrastructure.Docker;
using CompilerService.Infrastructure.Storage;

namespace CompilerService.Hosting;

public class DockerStartupService(
    DockerPool dockerPool,
    ILogger<DockerStartupService> logger,
    IConfiguration configuration
    ) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("DockerStartupService running...");
        
        try 
        {
            var numberOfWorkers = configuration.GetValue<int>(Constants.NumberOfWorkersSetting);
            await dockerPool.InitializeAsync(numberOfWorkers);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Can't initialize docker pool");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("DockerStartupService stopping...");
        return Task.CompletedTask;
    }
}
