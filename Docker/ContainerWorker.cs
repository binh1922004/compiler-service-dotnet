using System.Text;
using Docker.DotNet;
using Docker.DotNet.Models;

namespace CompilerService.Docker;

public class ContainerWorker(
    string containerId,
    DockerClient client
    )
{
    public async Task JudgeProblem(string[] exeCmd, string expectedOutput)
    {
        var execCreateResponse = await client.Exec.ExecCreateContainerAsync(containerId,
            new ContainerExecCreateParameters()
            {
                AttachStdout = true,
                AttachStderr = true,
                Cmd = exeCmd,
            });
        
        var stream = await client.Exec.StartAndAttachContainerExecAsync(
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
    }
    
    
}