using Amazon;
using Amazon.S3;
using CompilerService.Commands;
using CompilerService.Docker;
using CompilerService.Kafka;
using CompilerService.Services;
using CompilerService.Settings;
using CompilerService.Utilities;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<DockerStartupService>();
builder.Services.Configure<WorkSetting>(builder.Configuration.GetSection(Constant.WorkDirSetting));
builder.Services.Configure<KafkaSetting>(builder.Configuration.GetSection(Constant.KafkaSetting));
builder.Services.Configure<AwsS3Setting>(builder.Configuration.GetSection(Constant.AwsS3Setting));
var awsS3Setting = builder.Configuration.GetSection(Constant.AwsS3Setting).Get<AwsS3Setting>();

builder.Services.AddSingleton<DockerPool>();
builder.Services.AddSingleton<FileCommand>();
builder.Services.AddSingleton<CompilerCommand>();
builder.Services.AddSingleton<ICompileService, CompileService>();
builder.Services.AddSingleton<IS3Service, S3Service>();
builder.Services.AddSingleton<IFileService, FileService>();
builder.Services.AddHostedService<KafkaService>();

builder.Services.AddSingleton<IAmazonS3>(provider => new AmazonS3Client(awsS3Setting?.AccessKey, awsS3Setting?.SecretKey, RegionEndpoint.USEast1));

var host = builder.Build();
host.Run();