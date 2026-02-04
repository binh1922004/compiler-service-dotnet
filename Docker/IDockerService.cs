using Docker.DotNet.Models;

namespace CompilerService.Docker;

public interface IDockerService
{
    Task<DockerResponse> ExecCmdFromContainer(string containerId, string cmd);
    Task<string> CreateContainer(string name, CreateContainerParameters containerParameters);
    Task StartContainer(string containerId);
    Task StopContainer(string containerId);
    Task RemoveContainer(string containerId);
    // Return 1 if running, 0 if not start, -1 if not created
    Task<int> IsContainerRunning(string containerId);
}