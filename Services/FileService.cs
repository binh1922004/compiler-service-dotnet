namespace CompilerService.Services;

public class FileService(ILogger<FileService> logger) : IFileService
{
    public bool FolderExists(string path)
    {
        return Directory.Exists(path);
    }
}