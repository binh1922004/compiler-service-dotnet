using System.Collections.Concurrent;
using System.Text;
using Docker.DotNet;
using Docker.DotNet.Models;

namespace CompilerService.Docker;

public class DockerPool
{
    private readonly ILogger<DockerPool> _logger;
    private readonly DockerClient _client;
    private readonly string? _image;
    private readonly string? _version;
    private readonly string? _problemVolume;
    private readonly string? _submissionVolume;
    private readonly ConcurrentQueue<string> _availableContainers;
    // Limit 5 thread can run at time
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
            _logger.LogInformation("Constructor: Starting Docker by using Window");
            _client = new DockerClientConfiguration(
                    new Uri("npipe://./pipe/docker_engine"))
                .CreateClient();
        }
        else
        {
            _logger.LogInformation("Constructor: Starting Docker by using linux");
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
                _logger.LogInformation($"Checking existed container {i}");
                var container = await _client.Containers.InspectContainerAsync(GetContainerName(i));
                _logger.LogInformation($"Container {i} was created");

                if (!container.State.Running)
                {
                    await _client.Containers.StartContainerAsync(container.ID, new ContainerStartParameters());
                }
            }
            catch (DockerApiException e)
            {
                _logger.LogInformation($"Container {i} is not created");
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
        
        _logger.LogInformation($"Creating Container {id}");
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
        _logger.LogInformation($"Created Container {id}");
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
    
    public async Task ReturnContainerAsync(string containerId, CancellationToken cancellationToken = default)
    {
        _availableContainers.Enqueue(containerId);
        _semaphore.Release();
    }
    
    
    public async Task ExecCmdFromContainer(string containerId, string exeCmd, CancellationToken cancellationToken = default)
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

            var buffer = new byte[4096];

            while (!cancellationToken.IsCancellationRequested) 
            {
                var result = await stream.ReadOutputAsync(buffer, 0, buffer.Length, cancellationToken);
        
                if (result.EOF)
                    break;

                if (result.Count <= 0) continue;
        
                var text = Encoding.UTF8.GetString(buffer, 0, result.Count);
                Console.Write(text);
            }
        }
        finally
        {
            _semaphore.Release(); 
        }
    }
}