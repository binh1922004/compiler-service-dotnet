using CompilerService;
using CompilerService.Docker;
using CompilerService.Services;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<DockerStartupService>();

builder.Services.AddSingleton<DockerPool>();

var host = builder.Build();
host.Run();
