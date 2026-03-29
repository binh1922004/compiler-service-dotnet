using System.Collections.Concurrent;
using System.Text;
using Docker.DotNet;
using Docker.DotNet.Models;

namespace CompilerService.Infrastructure.Docker;

public class DockerPool
{
    private readonly ILogger<DockerPool> _logger;
    private readonly DockerClient _client;
    private readonly string? _image;
    private readonly string? _version;
    private readonly string? _problemVolume;
    private readonly string? _submissionVolume;
    private readonly ConcurrentQueue<string> _availableContainers;
    // Limit 5 threads can run at a time
    private readonly SemaphoreSlim _semaphore;
    private const int MaxContainers = 5;
    
    public DockerPool(ILogger<DockerPool> logger, IConfiguration configuration, string os = "linux")
    {
        _logger = logger;
        _image = configuration["CompilerConfig:Image"];
        _version = configuration["CompilerConfig:Version"];
        _problemVolume = configuration["CompilerConfig:ProblemVolume"];
        _submissionVolume = configuration["CompilerConfig:SubmissionVolume"];
        
        _availableContainers = new ConcurrentQueue<string>();
        _semaphore = new SemaphoreSlim(MaxContainers);
        
        if (os == "win")
        {
            _logger.LogInformation("Constructor: Starting Docker by using Windows");
            _client = new DockerClientConfiguration(
                    new Uri("npipe://./pipe/docker_engine"))
                .CreateClient();
        }
        else
        {
            _logger.LogInformation("Constructor: Starting Docker by using Linux");
            _client = new DockerClientConfiguration(
                    new Uri("unix:///var/run/docker.sock"))
                .CreateClient();
        }
    }
    

    public async Task InitializeAsync(int poolSize)
    {
        for (var i = 0; i < poolSize; i++)
        {
            try
            {
                _logger.LogInformation("Checking existing container {ContainerIndex}", i);
                var container = await _client.Containers.InspectContainerAsync(GetContainerName(i));
                _logger.LogInformation("Container {ContainerIndex} was created", i);

                if (!container.State.Running)
                {
                    await _client.Containers.StartContainerAsync(container.ID, new ContainerStartParameters());
                }
            }
            catch (DockerApiException)
            {
                _logger.LogInformation("Container {ContainerIndex} is not created", i);
                await CreateContainer(i);
            }
            finally
            {
                _availableContainers.Enqueue(GetContainerName(i));
            }
        }
        
    }

    private async Task CreateContainer(int id)
    {
        
        _logger.LogInformation("Creating Container {ContainerId}", id);
        var container = await _client.Containers.CreateContainerAsync(new CreateContainerParameters()
        {
            Image = $"{_image}:{_version}",
            Name = GetContainerName(id),
            Tty = false,
            WorkingDir = "/work",
            User = "0:0",
            HostConfig = new HostConfig()
            {
                NetworkMode = "none",
                Memory = 512 * 1024 * 1024,
                NanoCPUs = (long)1e9,
                PidsLimit = 128,
                ReadonlyRootfs = false,
                Binds = [
                    $"{_problemVolume}:/problems",
                    $"{_submissionVolume}:/work"
                ]
            },
            Cmd = ["/bin/bash", "-c", "sleep infinity"]
        });
        _logger.LogInformation("Created Container {ContainerId}", id);
        await _client.Containers.StartContainerAsync(container.ID, new ContainerStartParameters());
    }

    private static string GetContainerName(int id)
    {
        return $"bnoj-compiler-{id}";
    }


    public async Task<string?> RentContainerAsync()
    {
        await _semaphore.WaitAsync();
        return _availableContainers.TryDequeue(out var container) ? 
            container : 
            throw new Exception("No available containers");
    }
    
    public Task ReturnContainerAsync(string containerId, CancellationToken cancellationToken = default)
    {
        _availableContainers.Enqueue(containerId);
        _semaphore.Release();
        return Task.CompletedTask;
    }
    
    
    public async Task<string> ExecCmdFromContainer(string containerId, string exeCmd, CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            var execCreateResponse = await _client.Exec.ExecCreateContainerAsync(containerId,
                new ContainerExecCreateParameters()
                {
                    AttachStdout = true,
                    AttachStderr = true,
                    Cmd = ["/bin/sh", "-lc", exeCmd],
                    WorkingDir =  "/work",
                }, cancellationToken);
        
            using var stream = await _client.Exec.StartAndAttachContainerExecAsync(
                execCreateResponse.ID,
                false, cancellationToken);

            var outputBuilder = new StringBuilder();
            var buffer = new byte[4096];

            while (!cancellationToken.IsCancellationRequested) 
            {
                var result = await stream.ReadOutputAsync(buffer, 0, buffer.Length, cancellationToken);
        
                if (result.EOF)
                    break;

                if (result.Count <= 0) continue;
        
                var text = Encoding.UTF8.GetString(buffer, 0, result.Count);
                outputBuilder.Append(text);
            }

            var output = outputBuilder.ToString();
            _logger.LogDebug("Exec output for [{Command}]: {DockerOutput}", exeCmd, output);
            return output;
        }
        finally
        {
            _semaphore.Release(); 
        }
    }
}
