using System.IO.Compression;
using Amazon.S3;
using Amazon.S3.Model;
using CompilerService.Configuration;
using Microsoft.Extensions.Options;

namespace CompilerService.Infrastructure.Storage;

public class S3Service(
    IAmazonS3 client,
    IOptions<AwsS3Settings> awsS3Settings,
    IOptions<WorkSettings> workSettings,
    ILogger<S3Service> logger)
    : IS3Service
{
    private readonly AwsS3Settings _awsS3Settings = awsS3Settings.Value;
    private readonly WorkSettings _workSettings = workSettings.Value;

    public async Task DownloadFile(string key)
    {
        var request = new GetObjectRequest
        {
            Key = key,
            BucketName = _awsS3Settings.BucketName
        };
        using var response = await client.GetObjectAsync(request);
        await using var responseStream = response.ResponseStream;
        using var ms = new MemoryStream();
        await responseStream.CopyToAsync(ms);
        var rootDirectory = _workSettings.ProblemDir + $"/{key}";
        await using var zipArchive = new ZipArchive(ms, ZipArchiveMode.Read);

        foreach (var entry in zipArchive.Entries)
        {
            var destinationPath = Path.Combine(rootDirectory, entry.FullName);
            if (!destinationPath.StartsWith(Path.GetFullPath(rootDirectory), StringComparison.OrdinalIgnoreCase))
                continue;

            if (Path.GetFileName(destinationPath).Length == 0)
            {
                Directory.CreateDirectory(destinationPath);
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            await entry.ExtractToFileAsync(destinationPath, true);
        }
    }

    public async Task DownloadProblemFromS3Async(string key, string rootDirectory)
    {
        key = _awsS3Settings.ProblemPrefix + "/" + key;
        var request = new GetObjectRequest
        {
            Key = key,
            BucketName = _awsS3Settings.BucketName
        };

        logger.LogInformation("Downloading key {key} from S3", key);

        using var response = await client.GetObjectAsync(request);
        await using var responseStream = response.ResponseStream;
        using var ms = new MemoryStream();
        await responseStream.CopyToAsync(ms);

        if (!Directory.Exists(rootDirectory)) Directory.CreateDirectory(rootDirectory);

        await using var zipArchive = new ZipArchive(ms, ZipArchiveMode.Read);

        foreach (var entry in zipArchive.Entries)
        {
            var destinationPath = Path.Combine(rootDirectory, entry.FullName);
            if (Path.GetFileName(destinationPath).Length == 0)
            {
                Directory.CreateDirectory(destinationPath);
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            await entry.ExtractToFileAsync(destinationPath, true);
        }

        logger.LogInformation("Downloaded key {key} successfully", key);
    }

    public async Task<string> UploadFileAsync(string filePath, string key)
    {
        var request = new PutObjectRequest()
        {
            BucketName = _awsS3Settings.BucketName,
            Key = key,
            FilePath = filePath,
            ContentType = "application/zip"
        };
        var response = await client.PutObjectAsync(request);
        if (response.HttpStatusCode == System.Net.HttpStatusCode.OK)
        {
            logger.LogInformation("File {filePath} uploaded successfully with key {key}", filePath, key);
            return key;
        }
        else
        {
            logger.LogError("Failed to upload file {filePath} with key {key}. Status code: {statusCode}", filePath, key, response.HttpStatusCode);
            throw new Exception($"Failed to upload file. Status code: {response.HttpStatusCode}");
        }
    }
}