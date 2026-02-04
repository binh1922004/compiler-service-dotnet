using Docker.DotNet.Models;

namespace CompilerService.Docker;

public class DockerResponse
{
    public ContainerExecInspectResponse? ContainerExecInspectResponse { get; set; }
    public string? Output  { get; set; }
}