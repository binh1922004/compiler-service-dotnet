using System.Collections.Concurrent;
using System.Text;
using Docker.DotNet;
using Docker.DotNet.Models;

namespace CompilerService.Docker;

public class DockerPool
{
    private readonly ILogger<DockerPool> _logger;
    private readonly string? _image;
    private readonly string? _version;
    private readonly string? _problemVolume;
    private readonly string? _submissionVolume;
    private readonly ConcurrentQueue<string> _availableContainers;
    private readonly IDockerService _dockerService;
    // Limit 5 thread can run at time
    private readonly SemaphoreSlim _semaphore;
    private const int MaxContainers = 5;
    public DockerPool(ILogger<DockerPool> logger, IConfiguration configuration, IDockerService dockerService)
    {
        _logger = logger;
        _image = configuration["CompilerConfig:Image"];
        _version = configuration["CompilerConfig:Version"];
        _problemVolume = configuration["CompilerConfig:ProblemVolume"];
        _submissionVolume = configuration["CompilerConfig:SubmissionVolume"];
        _availableContainers = new ConcurrentQueue<string>();
        _dockerService = dockerService;
    }
    

    public async Task InitializeAsync(int poolSize)
    {
        for (var i = 0; i < poolSize; i++)
        {
            _logger.LogInformation($"Checking existed container {i}");
            var name = GetContainerName(i);
            var isRunning = await _dockerService.IsContainerRunning(name);
            switch (isRunning)
            {
                //check if container was not started
                case 0:
                    _dockerService.StartContainer(name);
                    break;
                case -1:
                    CreateContainerForCompiler(name);
                    _dockerService.StartContainer(name);
                    break;
            }
            _availableContainers.Enqueue(name);
        }

    }
    
    private async Task CreateContainerForCompiler(string name)
    {
        await _dockerService.CreateContainer(name, new CreateContainerParameters()
        {
            Image = $"{_image}:{_version}",
            Name = name,
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

    public async Task ReleaseContainer(string name)
    {
        _availableContainers.Enqueue(name);
    }
}