using CompilerService;
using CompilerService.Docker;
using CompilerService.Services;
using Docker.DotNet;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<DockerStartupService>();
builder.Services.AddSingleton<IDockerService, DockerService>();
builder.Services.AddSingleton<DockerPool>();

var host = builder.Build();
host.Run();
