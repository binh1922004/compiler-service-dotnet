using System.Text;
using Docker.DotNet;
using Docker.DotNet.Models;

namespace CompilerService.Docker;

public class DockerService : IDockerService
{
    private readonly DockerClient _client;
    private readonly ILogger<DockerService> _logger;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public DockerService(ILogger<DockerService> logger)
    {
        _logger = logger;
        var dockerUri = Environment.OSVersion.Platform == PlatformID.Win32NT
            ? new Uri("npipe://./pipe/docker_engine")
            : new Uri("unix:///var/run/docker.sock");
        
        var config = new DockerClientConfiguration(dockerUri);
        _client = config.CreateClient();
    }
    public async Task<DockerResponse> ExecCmdFromContainer(string containerId, string exeCmd)
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
        }
        Console.WriteLine(output.ToString());
        stream.Dispose();
        var inspectResponse = await _client.Exec.InspectContainerExecAsync(execCreateResponse.ID);
        if (inspectResponse.ExitCode != 0 || output.ToString().Contains("__CE__"))
        {
            // --- PHÁT HIỆN LỖI COMPILE TẠI ĐÂY ---
            Console.WriteLine($"\n[COMPILE ERROR] Exit Code: {inspectResponse.ExitCode}");
        }
        else 
        {
            Console.WriteLine("\n[SUCCESS] Success Compile.");
        }
        _semaphore.Release();

        return new DockerResponse()
        {
            ContainerExecInspectResponse = inspectResponse,
            Output = output.ToString()
        };
    }

    public async Task<string> CreateContainer(string name, CreateContainerParameters containerParameters)
    {
        _logger.LogInformation($"Creating Container {name}");
        var container = await _client.Containers.CreateContainerAsync(containerParameters);
        _logger.LogInformation($"Created Container {name}");
        return container.ID;
    }

    public async Task StartContainer(string containerId)
    {
        await _client.Containers.StartContainerAsync(containerId, new ContainerStartParameters());
        _logger.LogInformation("Started container {ContainerId}", containerId);
    }

    public async Task StopContainer(string containerId)
    {
        await _client.Containers.StopContainerAsync(containerId, new ContainerStopParameters());
        _logger.LogInformation("Stopped container {ContainerId}", containerId);
    }

    public async Task RemoveContainer(string containerId)
    {
        await _client.Containers.RemoveContainerAsync(containerId, new ContainerRemoveParameters { Force = true });
        _logger.LogInformation("Removed container {ContainerId}", containerId);
    }
    
    public async Task<int> IsContainerRunning(string containerId)
    {
        try
        {
            var container = await _client.Containers.InspectContainerAsync(containerId);
            return container.State.Running ? 1 : 0;
        }
        catch
        {
            return -1;
        }
    }
}