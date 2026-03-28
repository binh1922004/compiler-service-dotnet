using System.IO.Compression;
using Amazon.S3;
using Amazon.S3.Model;
using CompilerService.Settings;
using Microsoft.Extensions.Options;

namespace CompilerService.Services;

public class S3Service(
    IAmazonS3 client,
    IOptions<AwsS3Setting> awsS3Setting,
    IOptions<WorkSetting> workSetting,
    ILogger<S3Service> logger)
    : IS3Service
{
    private readonly AwsS3Setting _awsS3Setting = awsS3Setting.Value;
    private readonly WorkSetting _workSetting = workSetting.Value;
    private readonly ILogger<S3Service> _logger = logger;

    public async Task DownloadFile(string key)
    {
        GetObjectRequest request = new GetObjectRequest
        {
            Key = key,
            BucketName = _awsS3Setting.BucketName
        };
        using var response = await client.GetObjectAsync(request);
        await using var responseStream = response.ResponseStream;
        using var ms = new MemoryStream();
        await responseStream.CopyToAsync(ms);
        var rootDirectory = _workSetting.ProblemDir + $"/{key}";
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

            // 4. Đảm bảo thư mục cha tồn tại và giải nén file
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath));
            await entry.ExtractToFileAsync(destinationPath, overwrite: true); 
        }
    }

    public async Task DownloadProblemFromS3Async(string problemId, string rootDirectory)
    {
        var key = _awsS3Setting.ProblemPrefix + $"/{problemId}/{problemId}.zip"; 
        var request = new GetObjectRequest
        {
            Key = key,
            BucketName = _awsS3Setting.BucketName
        };
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
            if (!destinationPath.StartsWith(Path.GetFullPath(rootDirectory), StringComparison.OrdinalIgnoreCase))
            {
                continue; 
            }

            if (Path.GetFileName(destinationPath).Length == 0)
            {
                Directory.CreateDirectory(destinationPath);
                continue;
            }

            // 4. Đảm bảo thư mục cha tồn tại và giải nén file
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath));
            await entry.ExtractToFileAsync(destinationPath, overwrite: true); 
        }
    }
}