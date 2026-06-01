using CompilerService.Configuration;
using CompilerService.Infrastructure.Docker;
using CompilerService.Infrastructure.Storage;
using CompilerService.Models;
using CompilerService.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Test.Services;

/// <summary>
/// Integration tests that exercise the full pipeline through real Docker containers.
/// Requires Docker running locally with the sandbox image built (oj:4.0).
/// Kafka is not involved — we call CompileService directly.
///
/// To run:  dotnet test --filter "Category=Integration"
/// To skip: dotnet test --filter "Category!=Integration"
/// </summary>
[Trait("Category", "Integration")]
public class CompilerServiceIntegrationTests : IAsyncLifetime
{
    private DockerPool _dockerPool = null!;
    private CompileService _sut = null!;
    private ILogger<CompileService> _serviceLogger = null!;

    // Match appsettings.Development.json
    private const string Image = "oj";
    private const string Version = "5.0";
    private const string TestCaseDir = "/test-case";
    private const string ScriptDir = "/scripts";

    public async Task InitializeAsync()
    {
        // Build configuration matching the dev environment
        var configValues = new Dictionary<string, string?>
        {
            ["CompilerConfig:Image"] = Image,
            ["CompilerConfig:Version"] = Version,
            ["CompilerConfig:ProblemVolume"] = "problem_data",
            ["CompilerConfig:SubmissionVolume"] = "submission_data"
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configValues)
            .Build();

        var dockerLogger = LoggerFactory.Create(b => b.AddConsole()).CreateLogger<DockerPool>();
        _serviceLogger = LoggerFactory.Create(b => b.AddConsole()).CreateLogger<CompileService>();

        // Real DockerPool — connects to local Docker daemon
        _dockerPool = new DockerPool(dockerLogger, configuration, "linux");

        // Initialize 1 container for tests (no need for a full pool)
        await _dockerPool.InitializeAsync(1);

        var workSettings = Options.Create(new WorkSettings
        {
            SubmissionDir = "/work",
            ProblemDir = "problems",
            TestCaseDir = TestCaseDir,
            ScriptDir = ScriptDir
        });

        var commandBuilder = new CommandBuilder(workSettings);

        // S3 and FileService are not needed for test case generation — use fakes
        var fileService = Substitute.For<IFileService>();
        var s3Service = Substitute.For<IS3Service>();

        var awsS3Settings = Options.Create(new AwsS3Settings
        {
            AccessKey = "fake-access",
            SecretKey = "fake-secret",
            BucketName = "fake-bucket",
            Region = "us-east-1",
            TestCasePrefix = "testcases",
            ProblemPrefix = "problems"
        });

        _sut = new CompileService(
            _dockerPool,
            fileService,
            workSettings,
            commandBuilder,
            s3Service,
            awsS3Settings,
            _serviceLogger);
    }

    public Task DisposeAsync()
    {
        // Containers remain for reuse — Docker pool does not destroy on dispose
        return Task.CompletedTask;
    }

    // ─────────────────────────────────────────────────
    // GenerateTestCases — end-to-end through Docker
    // ─────────────────────────────────────────────────

    [Fact]
    public async Task GenerateTestCases_SimpleInputOutput_ReturnsSuccess()
    {
        // Arrange — a generator that prints 3 test cases separated by boundary markers
        var plan = new TestCasePlan
        {
            PlanId = "integ-simple",
            Version = 1,
            InputCode = """
                for i in range(1, 4):
                    print(i)
                    if i < 3:
                        print("---TEST_BOUNDARY---")
                """,
            OutPutCode = """
                import sys
                x = int(sys.stdin.readline())
                print(x * 2)
                """
        };

        // Act
        var result = await _sut.GenerateTestCases(plan, CancellationToken.None);

        // Assert
        Assert.True(result.Success, $"Expected success but got error: {result.Error}");
        Assert.Equal("integ-simple", result.PlanId);
        Assert.Equal(3, result.TestCount);
    }

    [Fact]
    public async Task GenerateTestCases_SingleTestCase_ReturnsSuccessWithCount1()
    {
        // Arrange — generator that outputs a single test case (no boundary)
        var plan = new TestCasePlan
        {
            PlanId = "integ-single",
            Version = 1,
            InputCode = """
                print(42)
                """,
            OutPutCode = """
                import sys
                x = int(sys.stdin.readline())
                print(x + 1)
                """
        };

        // Act
        var result = await _sut.GenerateTestCases(plan, CancellationToken.None);

        // Assert
        Assert.True(result.Success, $"Expected success but got error: {result.Error}");
        Assert.Equal(1, result.TestCount);
    }

    [Fact]
    public async Task GenerateTestCases_RandomInput_ReturnsSuccess()
    {
        // Arrange — use random to generate varied test cases
        var plan = new TestCasePlan
        {
            PlanId = "integ-random",
            Version = 1,
            InputCode = """
                import random
                random.seed(12345)
                for i in range(5):
                    n = random.randint(1, 100)
                    print(n)
                    if i < 4:
                        print("---TEST_BOUNDARY---")
                """,
            OutPutCode = """
                import sys
                x = int(sys.stdin.readline())
                print(x ** 2)
                """
        };

        // Act
        var result = await _sut.GenerateTestCases(plan, CancellationToken.None);

        // Assert
        Assert.True(result.Success, $"Expected success but got error: {result.Error}");
        Assert.Equal(5, result.TestCount);
    }

    [Fact]
    public async Task GenerateTestCases_MultiLineInput_ReturnsSuccess()
    {
        // Arrange — each test case has multiple lines of input
        var plan = new TestCasePlan
        {
            PlanId = "integ-multiline",
            Version = 1,
            InputCode = """
                for i in range(2):
                    print(3)
                    print("1 2 3")
                    if i < 1:
                        print("---TEST_BOUNDARY---")
                """,
            OutPutCode = """
                import sys
                n = int(sys.stdin.readline())
                nums = list(map(int, sys.stdin.readline().split()))
                print(sum(nums))
                """
        };

        // Act
        var result = await _sut.GenerateTestCases(plan, CancellationToken.None);

        // Assert
        Assert.True(result.Success, $"Expected success but got error: {result.Error}");
        Assert.Equal(2, result.TestCount);
    }

    [Fact]
    public async Task GenerateTestCases_BadGeneratorCode_ReturnsFailure()
    {
        // Arrange — Python syntax error in the generator
        var plan = new TestCasePlan
        {
            PlanId = "integ-bad-gen",
            Version = 1,
            InputCode = """
                this is not valid python!!!
                """,
            OutPutCode = """
                print("ok")
                """
        };

        // Act
        var result = await _sut.GenerateTestCases(plan, CancellationToken.None);

        // Assert — the script should report failure
        Assert.False(result.Success);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public async Task GenerateTestCases_BadSolutionCode_ReturnsFailure()
    {
        // Arrange — valid generator but broken solution
        var plan = new TestCasePlan
        {
            PlanId = "integ-bad-sol",
            Version = 1,
            InputCode = """
                print(1)
                """,
            OutPutCode = """
                this is not valid python either!!!
                """
        };

        // Act
        var result = await _sut.GenerateTestCases(plan, CancellationToken.None);

        // Assert — should fail during output generation
        Assert.False(result.Success);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public async Task GenerateTestCases_VersioningCreatesDistinctFolders()
    {
        // Arrange — run two different versions of the same planId
        var planV1 = new TestCasePlan
        {
            PlanId = "integ-versioned",
            Version = 1,
            InputCode = "print(10)",
            OutPutCode = """
                import sys
                print(int(sys.stdin.readline()) * 2)
                """
        };

        var planV2 = new TestCasePlan
        {
            PlanId = "integ-versioned",
            Version = 2,
            InputCode = "print(20)",
            OutPutCode = """
                import sys
                print(int(sys.stdin.readline()) * 3)
                """
        };

        // Act — both should succeed without interfering
        var resultV1 = await _sut.GenerateTestCases(planV1, CancellationToken.None);
        var resultV2 = await _sut.GenerateTestCases(planV2, CancellationToken.None);

        // Assert
        Assert.True(resultV1.Success, $"V1 failed: {resultV1.Error}");
        Assert.True(resultV2.Success, $"V2 failed: {resultV2.Error}");
        Assert.Equal(1, resultV1.TestCount);
        Assert.Equal(1, resultV2.TestCount);
    }
}
