namespace CompilerService.Services;

public interface IS3Service
{
    Task DownloadFile(string key);
    Task DownloadProblemFromS3Async(string problemId, string rootDirectory);
}