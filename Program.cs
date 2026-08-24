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
using Google.Apis.Auth.OAuth2;
using Google.Apis.Download;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Google.Apis.Util.Store;

var builder = WebApplication.CreateBuilder(args);

// Configuration
builder.Services.Configure<WorkSettings>(builder.Configuration.GetSection(Constants.WorkDirSetting));
builder.Services.Configure<KafkaSettings>(builder.Configuration.GetSection(Constants.KafkaSetting));
builder.Services.Configure<AwsS3Settings>(builder.Configuration.GetSection(Constants.AwsS3Setting));
builder.Services.Configure<KafkaAuthentication>(builder.Configuration.GetSection(Constants.KafkaAuthenticationSettings));
var awsS3Settings = builder.Configuration.GetSection(Constants.AwsS3Setting).Get<AwsS3Settings>();

// Infrastructure
// builder.Services.AddSingleton<DockerPool>();
builder.Services.AddSingleton<IAmazonS3>(_ => new AmazonS3Client(awsS3Settings?.AccessKey, awsS3Settings?.SecretKey, RegionEndpoint.USEast1));
builder.Services.AddSingleton<IS3Service, S3Service>();
// builder.Services.AddSingleton<IKafkaProducer, KafkaProducer>();
// builder.Services.AddSingleton<IKafkaClient, KafkaClient>();

// Services
builder.Services.AddSingleton<CommandBuilder>();
// builder.Services.AddSingleton<ICompileService, CompileService>();
builder.Services.AddSingleton<IFileService, FileService>();

// Message Handlers
// builder.Services.AddSingleton<IMessageHandler<SubmissionRequest>, SubmissionHandler>();
// builder.Services.AddSingleton<IMessageHandler<TestCasePlan>, TestCaseGenerationHandler>();
// builder.Services.AddSingleton<IMessageHandler<PreTestRequest>, PreTestHandler>();

// Hosted Services
// builder.Services.AddHostedService<DockerStartupService>();
// builder.Services.AddHostedService<KafkaSubscriberWorker>();

var host = builder.Build();

host.MapGet("/api/file/{fileId}", (string fileId) =>
{
    try
    {
        string[] Scopes = { DriveService.Scope.DriveReadonly };
        string ApplicationName = "Testing";
        UserCredential credential;

        // Load client secrets
        using (var fileStream = new FileStream("/Users/mac/Downloads/credentials.json", FileMode.Open, FileAccess.Read))
        {
            string credPath = "token.json";

            credential = GoogleWebAuthorizationBroker.AuthorizeAsync(
                GoogleClientSecrets.FromStream(fileStream).Secrets,
                Scopes,
                "user",
                CancellationToken.None,
                new FileDataStore(credPath, true)).Result;
        }

        var service = new DriveService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "Testing"
        });

        var request = service.Files.Get(fileId);

        // Fetch file metadata to get the original file name and MIME type
        var fileMetadata = request.Execute();
        var fileName = !string.IsNullOrEmpty(fileMetadata.Name) ? fileMetadata.Name : "downloaded_file";
        var mimeType = !string.IsNullOrEmpty(fileMetadata.MimeType) ? fileMetadata.MimeType : "application/octet-stream";

        var stream = new MemoryStream();

        request.MediaDownloader.ProgressChanged +=
            progress =>
            {
                switch (progress.Status)
                {
                    case DownloadStatus.Downloading:
                        {
                            Console.WriteLine(progress.BytesDownloaded);
                            break;
                        }
                    case DownloadStatus.Completed:
                        {
                            Console.WriteLine("Download complete.");
                            break;
                        }
                    case DownloadStatus.Failed:
                        {
                            Console.WriteLine("Download failed.");
                            break;
                        }
                }
            };

        request.Download(stream);

        // --- THE FIX IS HERE --
        // 1. Reset the position to the beginning of the stream
        stream.Position = 0;

        // 2. Return as a file result using the original file name and MIME type
        return Results.File(stream, mimeType, fileName);
    }
    catch (Exception e)
    {
        if (e is AggregateException)
        {
            Console.WriteLine("Credential Not found");
            return Results.Problem("Credential Not found", statusCode: 500);
        }

        // Return a 500 Internal Server Error instead of throwing directly
        return Results.Problem(e.Message, statusCode: 500);
    }
});

host.Run();