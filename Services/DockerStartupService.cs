using CompilerService.Docker;
using Docker.DotNet;

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
            await dockerPool.InitializeAsync(1);
            // Console.WriteLine(dockerClient.Containers.ToString());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Can't initialize docker pool");
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        // await dockerPool.ReleaseAsync();
    }
}