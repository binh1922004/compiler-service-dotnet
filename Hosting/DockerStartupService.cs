using CompilerService.Infrastructure.Docker;
using CompilerService.Infrastructure.Storage;

namespace CompilerService.Hosting;

public class DockerStartupService(
    DockerPool dockerPool,
    ILogger<DockerStartupService> logger,
    IS3Service s3Service
    ) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("DockerStartupService running...");
        
        try 
        {
            await dockerPool.InitializeAsync(3);
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
