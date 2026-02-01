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

        await Testing();

    }

    private async Task Testing()
    {
        _logger.LogInformation("Testing Docker");
        string cmd =
            "g++ -std=gnu++17 -O2 -pipe -static -s /work/696bb1f1053d681143c4c3ab/Main.cpp -o /work/696bb1f1053d681143c4c3ab/Main || echo __CE__:$? >&2";
        await execCmdFromContainer(GetContainerName(3), cmd);

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
    
    
    public async Task execCmdFromContainer(string containerId, string exeCmd)
    {
        await _semaphore.WaitAsync();
        var execCreateResponse = await _client.Exec.ExecCreateContainerAsync(containerId,
            new ContainerExecCreateParameters()
            {
                AttachStdout = true,
                AttachStderr = true,
                Cmd = ["/bin/sh", "-lc", exeCmd],
                WorkingDir =  "/work",
            });
        
        var stream = await _client.Exec.StartAndAttachContainerExecAsync(
            execCreateResponse.ID,
            false);

        var output = new StringBuilder();
        var buffer = new byte[4096];

        while (true)
        {
            var result = await stream.ReadOutputAsync(buffer, 0, buffer.Length, CancellationToken.None);
    
            if (result.EOF)
                break;

            if (result.Count <= 0) continue;
            var text = Encoding.UTF8.GetString(buffer, 0, result.Count);
            output.Append(text);
            Console.Write(text); // In ra realtime
        }
        Console.WriteLine(output.ToString());
        stream.Dispose();
        var inspectResponse = await _client.Exec.InspectContainerExecAsync(execCreateResponse.ID);
        if (inspectResponse.ExitCode != 0)
        {
            // --- PHÁT HIỆN LỖI COMPILE TẠI ĐÂY ---
            Console.WriteLine($"\n[COMPILE ERROR] Exit Code: {inspectResponse.ExitCode}");
            Console.WriteLine("Chi tiết lỗi:");
        }
        else 
        {
            Console.WriteLine("\n[SUCCESS] Biên dịch thành công.");
        }
        _semaphore.Release();
    }
}