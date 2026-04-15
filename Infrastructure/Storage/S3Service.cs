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
            {
                continue; 
            }

            if (Path.GetFileName(destinationPath).Length == 0)
            {
                Directory.CreateDirectory(destinationPath);
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            await entry.ExtractToFileAsync(destinationPath, overwrite: true); 
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
       
        if (!Directory.Exists(rootDirectory))
        {
            Directory.CreateDirectory(rootDirectory);
        }
        
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
            await entry.ExtractToFileAsync(destinationPath, overwrite: true); 
        }
        
        logger.LogInformation("Downloaded key {key} successfully", key);
    }
}
