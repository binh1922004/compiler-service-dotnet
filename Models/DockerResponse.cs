using Docker.DotNet.Models;

namespace CompilerService.Models;

public class DockerResponse
{
    public ContainerExecInspectResponse? ContainerExecInspectResponse { get; set; }
    public string? Output  { get; set; }
}
