using CompilerService.Docker;
using CompilerService.DTO;
using CompilerService.Utilities;

namespace CompilerService.Services;

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
        throw new NotImplementedException();
    }
}