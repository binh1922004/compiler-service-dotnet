using Amazon;
using Amazon.S3;
using CompilerService.Configuration;
using CompilerService.Hosting;
using CompilerService.Infrastructure.Docker;
using CompilerService.Infrastructure.Kafka;
using CompilerService.Infrastructure.Kafka.Handlers;
using CompilerService.Infrastructure.Storage;
using CompilerService.Models;
using CompilerService.Services;

var builder = Host.CreateApplicationBuilder(args);

// Configuration
builder.Services.Configure<WorkSettings>(builder.Configuration.GetSection(Constants.WorkDirSetting));
builder.Services.Configure<KafkaSettings>(builder.Configuration.GetSection(Constants.KafkaSetting));
builder.Services.Configure<AwsS3Settings>(builder.Configuration.GetSection(Constants.AwsS3Setting));
var awsS3Settings = builder.Configuration.GetSection(Constants.AwsS3Setting).Get<AwsS3Settings>();

// Infrastructure
builder.Services.AddSingleton<DockerPool>();
builder.Services.AddSingleton<IAmazonS3>(_ => new AmazonS3Client(awsS3Settings?.AccessKey, awsS3Settings?.SecretKey, RegionEndpoint.USEast1));
builder.Services.AddSingleton<IS3Service, S3Service>();
builder.Services.AddSingleton<IKafkaProducer, KafkaProducer>();
builder.Services.AddSingleton<IKafkaClient, KafkaClient>();

// Services
builder.Services.AddSingleton<CommandBuilder>();
builder.Services.AddSingleton<ICompileService, CompileService>();
builder.Services.AddSingleton<IFileService, FileService>();

// Message Handlers
builder.Services.AddSingleton<IMessageHandler<SubmissionRequest>, SubmissionHandler>();

// Hosted Services
builder.Services.AddHostedService<DockerStartupService>();
builder.Services.AddHostedService<KafkaConsumerService>();
builder.Services.AddHostedService<KafkaSubscriberWorker>();

var host = builder.Build();
host.Run();