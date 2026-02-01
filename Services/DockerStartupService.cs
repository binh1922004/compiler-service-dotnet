using CompilerService.Docker;

namespace CompilerService.Services;

public class DockerStartupService(
    DockerPool dockerPool,
    ILogger<DockerStartupService> logger
    ) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("DockerStartupService running...");
        
        try 
        {
            await dockerPool.InitializeAsync(5);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Can't initialize docker pool");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}