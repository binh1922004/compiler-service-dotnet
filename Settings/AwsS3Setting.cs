namespace CompilerService.Settings;

public class AwsS3Setting
{
    public string AccessKey { get; set; }
    public string SecretKey { get; set; }
    public string BucketName { get; set; }
    public string Region { get; set; }
    
    public string ProblemPrefix { get; set; }
}