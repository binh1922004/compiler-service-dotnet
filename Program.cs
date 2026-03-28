using Amazon;
using Amazon.S3;
using CompilerService.Configuration;
using CompilerService.Hosting;
using CompilerService.Infrastructure.Docker;
using CompilerService.Infrastructure.Kafka;
using CompilerService.Infrastructure.Storage;
using CompilerService.Services;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<DockerStartupService>();
builder.Services.Configure<WorkSettings>(builder.Configuration.GetSection(Constants.WorkDirSetting));
builder.Services.Configure<KafkaSettings>(builder.Configuration.GetSection(Constants.KafkaSetting));
builder.Services.Configure<AwsS3Settings>(builder.Configuration.GetSection(Constants.AwsS3Setting));
var awsS3Settings = builder.Configuration.GetSection(Constants.AwsS3Setting).Get<AwsS3Settings>();

builder.Services.AddSingleton<DockerPool>();
builder.Services.AddSingleton<CommandBuilder>();
builder.Services.AddSingleton<ICompileService, CompileService>();
builder.Services.AddSingleton<IS3Service, S3Service>();
builder.Services.AddSingleton<IFileService, FileService>();
builder.Services.AddHostedService<KafkaConsumerService>();

builder.Services.AddSingleton<IAmazonS3>(provider => new AmazonS3Client(awsS3Settings?.AccessKey, awsS3Settings?.SecretKey, RegionEndpoint.USEast1));

var host = builder.Build();
host.Run();