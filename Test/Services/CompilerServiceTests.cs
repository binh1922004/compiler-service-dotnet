using CompilerService.Configuration;
using CompilerService.Infrastructure.Docker;
using CompilerService.Infrastructure.Storage;
using CompilerService.Models;
using CompilerService.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Test.Services;

public class CompilerServiceTests
{
    private readonly DockerPool _dockerPool;
    private readonly IFileService _fileService;
    private readonly CommandBuilder _commandBuilder;
    private readonly IS3Service _s3Service;
    private readonly ILogger<CompileService> _logger;
    private readonly CompileService _sut;

    private const string FakeContainerId = "bnoj-compiler-0";

    public CompilerServiceTests()
    {
        _dockerPool = Substitute.ForPartsOf<DockerPool>(
            Substitute.For<ILogger<DockerPool>>(),
            BuildFakeConfiguration(),
            "linux");

        // Stub the Docker methods so no real Docker calls happen
        _dockerPool.RentContainerAsync().Returns(Task.FromResult<string?>(FakeContainerId));
        _dockerPool.ReturnContainerAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        _dockerPool.ExecCmdFromContainer(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(string.Empty));

        _fileService = Substitute.For<IFileService>();
        _s3Service = Substitute.For<IS3Service>();
        _logger = Substitute.For<ILogger<CompileService>>();

        var workSettings = Options.Create(new WorkSettings
        {
            SubmissionDir = "/work",
            ProblemDir = "problems",
            TestCaseDir = "/test-case",
            ScriptDir = "/scripts"
        });

        _commandBuilder = new CommandBuilder(workSettings);

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
            _fileService,
            workSettings,
            _commandBuilder,
            _s3Service,
            awsS3Settings,
            _logger);
    }

    private static IConfiguration BuildFakeConfiguration()
    {
        var inMemory = new Dictionary<string, string?>
        {
            ["CompilerConfig:Image"] = "oj",
            ["CompilerConfig:Version"] = "4.0",
            ["CompilerConfig:ProblemVolume"] = "problem_data",
            ["CompilerConfig:SubmissionVolume"] = "submission_data"
        };

        return new ConfigurationBuilder()
            .AddInMemoryCollection(inMemory)
            .Build();
    }

    // ─────────────────────────────────────────────────
    // GenerateTestCases — success scenarios
    // ─────────────────────────────────────────────────

    [Fact]
    public async Task GenerateTestCases_SuccessfulRun_ReturnsSuccessResult()
    {
        // Arrange
        var plan = CreateTestCasePlan();
        var scriptJson = """{"status":"success","zipPath":"/test-case/plan-123-v1/testcases.zip","testCount":5}""";

        // The second ExecCmdFromContainer call (the generator script) returns JSON
        _dockerPool.ExecCmdFromContainer(FakeContainerId, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(
                Task.FromResult(string.Empty),   // CreateTestCaseFilesCommand
                Task.FromResult(scriptJson),      // GenerateTestCaseCommand
                Task.FromResult(string.Empty));   // DeleteTestCaseFolderCommand

        // Act
        var result = await _sut.GenerateTestCases(plan, CancellationToken.None);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("plan-123", result.PlanId);
        Assert.Equal(5, result.TestCount);
        Assert.Null(result.Error);
    }

    [Fact]
    public async Task GenerateTestCases_SuccessfulRun_RentsAndReturnsContainer()
    {
        // Arrange
        var plan = CreateTestCasePlan();
        var scriptJson = """{"status":"success","zipPath":"/test-case/plan-123-v1/testcases.zip","testCount":3}""";

        _dockerPool.ExecCmdFromContainer(FakeContainerId, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(
                Task.FromResult(string.Empty),
                Task.FromResult(scriptJson),
                Task.FromResult(string.Empty));

        // Act
        await _sut.GenerateTestCases(plan, CancellationToken.None);

        // Assert — container lifecycle
        await _dockerPool.Received(1).RentContainerAsync();
        await _dockerPool.Received(1).ReturnContainerAsync(FakeContainerId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GenerateTestCases_SuccessfulRun_ExecutesThreeDockerCommands()
    {
        // Arrange
        var plan = CreateTestCasePlan();
        var scriptJson = """{"status":"success","zipPath":"/test-case/plan-123-v1/testcases.zip","testCount":3}""";

        _dockerPool.ExecCmdFromContainer(FakeContainerId, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(
                Task.FromResult(string.Empty),
                Task.FromResult(scriptJson),
                Task.FromResult(string.Empty));

        // Act
        await _sut.GenerateTestCases(plan, CancellationToken.None);

        // Assert — 3 calls: create files, run generator, delete folder
        await _dockerPool.Received(3)
            .ExecCmdFromContainer(FakeContainerId, Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    // ─────────────────────────────────────────────────
    // GenerateTestCases — script failure scenarios
    // ─────────────────────────────────────────────────

    [Fact]
    public async Task GenerateTestCases_ScriptReturnsFailure_ReturnsErrorResult()
    {
        // Arrange
        var plan = CreateTestCasePlan();
        var scriptJson = """{"status":"failed","error":"Input generator failed with exit code 1"}""";

        _dockerPool.ExecCmdFromContainer(FakeContainerId, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(
                Task.FromResult(string.Empty),
                Task.FromResult(scriptJson),
                Task.FromResult(string.Empty));

        // Act
        var result = await _sut.GenerateTestCases(plan, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("plan-123", result.PlanId);
        Assert.Equal("Input generator failed with exit code 1", result.Error);
        Assert.Equal(0, result.TestCount);
    }

    [Fact]
    public async Task GenerateTestCases_ScriptReturnsUnparsableOutput_ReturnsParseError()
    {
        // Arrange
        var plan = CreateTestCasePlan();
        const string garbageOutput = "Segmentation fault (core dumped)";

        _dockerPool.ExecCmdFromContainer(FakeContainerId, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(
                Task.FromResult(string.Empty),
                Task.FromResult(garbageOutput),
                Task.FromResult(string.Empty));

        // Act
        var result = await _sut.GenerateTestCases(plan, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("Failed to parse script output", result.Error);
    }

    [Fact]
    public async Task GenerateTestCases_ScriptOutputWithStderrNoise_ParsesJsonCorrectly()
    {
        // Arrange — stderr lines mixed in before the JSON
        var plan = CreateTestCasePlan();
        var mixedOutput = """
            [*] Workspace ready: /test-case/plan-123-v1/testcases
            [*] Running input generator...
            {"status":"success","zipPath":"/test-case/plan-123-v1/testcases.zip","testCount":10}
            """;

        _dockerPool.ExecCmdFromContainer(FakeContainerId, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(
                Task.FromResult(string.Empty),
                Task.FromResult(mixedOutput),
                Task.FromResult(string.Empty));

        // Act
        var result = await _sut.GenerateTestCases(plan, CancellationToken.None);

        // Assert — should parse the JSON line despite noise
        Assert.True(result.Success);
        Assert.Equal(10, result.TestCount);
    }

    [Fact]
    public async Task GenerateTestCases_EmptyOutput_ReturnsParseError()
    {
        // Arrange
        var plan = CreateTestCasePlan();

        _dockerPool.ExecCmdFromContainer(FakeContainerId, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(
                Task.FromResult(string.Empty),
                Task.FromResult(string.Empty),
                Task.FromResult(string.Empty));

        // Act
        var result = await _sut.GenerateTestCases(plan, CancellationToken.None);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("Failed to parse script output", result.Error);
    }

    // ─────────────────────────────────────────────────
    // GenerateTestCases — exception scenarios
    // ─────────────────────────────────────────────────

    [Fact]
    public async Task GenerateTestCases_DockerExecThrows_ReturnsErrorAndStillReturnsContainer()
    {
        // Arrange
        var plan = CreateTestCasePlan();

        var callCount = 0;
        _dockerPool.ExecCmdFromContainer(FakeContainerId, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                callCount++;
                // Throw on the 1st call (CreateTestCaseFilesCommand), let cleanup succeed
                if (callCount == 1) throw new Exception("Container OOM killed");
                return Task.FromResult(string.Empty);
            });

        // Act
        var result = await _sut.GenerateTestCases(plan, CancellationToken.None);

        // Assert — should catch the exception and return a failure result
        Assert.False(result.Success);
        Assert.Equal("plan-123", result.PlanId);
        Assert.Equal("Container OOM killed", result.Error);

        // Container must still be returned (finally block)
        await _dockerPool.Received(1).ReturnContainerAsync(FakeContainerId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GenerateTestCases_ScriptFailure_StillCleansUpFolder()
    {
        // Arrange
        var plan = CreateTestCasePlan();
        var scriptJson = """{"status":"failed","error":"solution crashed"}""";

        _dockerPool.ExecCmdFromContainer(FakeContainerId, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(
                Task.FromResult(string.Empty),
                Task.FromResult(scriptJson),
                Task.FromResult(string.Empty));

        // Act
        await _sut.GenerateTestCases(plan, CancellationToken.None);

        // Assert — cleanup (3rd exec) and return container should always happen
        await _dockerPool.Received(3)
            .ExecCmdFromContainer(FakeContainerId, Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _dockerPool.Received(1).ReturnContainerAsync(FakeContainerId, Arg.Any<CancellationToken>());
    }

    // ─────────────────────────────────────────────────
    // GenerateTestCases — version formatting
    // ─────────────────────────────────────────────────

    [Theory]
    [InlineData(1, "plan-123-v1")]
    [InlineData(2, "plan-123-v2")]
    [InlineData(10, "plan-123-v10")]
    public async Task GenerateTestCases_VersionVariations_UseCorrectPlanName(int version, string expectedPlanName)
    {
        // Arrange
        var plan = new TestCasePlan
        {
            PlanId = "plan-123",
            Version = version,
            InputCode = "print(1)",
            OutPutCode = "x = int(input())\nprint(x)"
        };

        var scriptJson = """{"status":"success","zipPath":"/test-case/testcases.zip","testCount":1}""";

        _dockerPool.ExecCmdFromContainer(FakeContainerId, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(
                Task.FromResult(string.Empty),
                Task.FromResult(scriptJson),
                Task.FromResult(string.Empty));

        // Act
        await _sut.GenerateTestCases(plan, CancellationToken.None);

        // Assert — the generator command should contain the versioned plan name
        await _dockerPool.Received(1).ExecCmdFromContainer(
            FakeContainerId,
            Arg.Is<string>(cmd => cmd.Contains(expectedPlanName) && cmd.Contains("test_case_generator.py")),
            Arg.Any<CancellationToken>());
    }

    // ─────────────────────────────────────────────────
    // SubmitCode — success
    // ─────────────────────────────────────────────────

    [Fact]
    public async Task SubmitCode_SuccessfulJudge_ReturnsDeserializedResponse()
    {
        // Arrange
        var request = CreateSubmissionRequest();
        _fileService.FolderExists(Arg.Any<string>()).Returns(true);

        var judgeJson = """{"submissionId":"sub-1","status":"AC","passed":10,"total":10,"max_time":0.5,"max_memory_mb":32.0}""";

        _dockerPool.ExecCmdFromContainer(FakeContainerId, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(
                Task.FromResult(string.Empty),  // CreateFile
                Task.FromResult(judgeJson),      // JudgeCode
                Task.FromResult(string.Empty));  // DeleteSubmissionFolder

        // Act
        var result = await _sut.SubmitCode(request, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("sub-1", result.Id);
        Assert.Equal("AC", result.Status);
        Assert.Equal(10, result.Passed);
        Assert.Equal(10, result.Total);
    }

    [Fact]
    public async Task SubmitCode_ProblemNotCached_DownloadsFromS3()
    {
        // Arrange
        var request = CreateSubmissionRequest();
        _fileService.FolderExists(Arg.Any<string>()).Returns(false);

        var judgeJson = """{"submissionId":"sub-1","status":"AC","passed":5,"total":5,"max_time":0.1,"max_memory_mb":16.0}""";

        _dockerPool.ExecCmdFromContainer(FakeContainerId, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(
                Task.FromResult(string.Empty),
                Task.FromResult(judgeJson),
                Task.FromResult(string.Empty));

        // Act
        await _sut.SubmitCode(request, CancellationToken.None);

        // Assert
        await _s3Service.Received(1).DownloadProblemFromS3Async(Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task SubmitCode_ProblemAlreadyCached_SkipsS3Download()
    {
        // Arrange
        var request = CreateSubmissionRequest();
        _fileService.FolderExists(Arg.Any<string>()).Returns(true);

        var judgeJson = """{"submissionId":"sub-1","status":"AC","passed":5,"total":5,"max_time":0.1,"max_memory_mb":16.0}""";

        _dockerPool.ExecCmdFromContainer(FakeContainerId, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(
                Task.FromResult(string.Empty),
                Task.FromResult(judgeJson),
                Task.FromResult(string.Empty));

        // Act
        await _sut.SubmitCode(request, CancellationToken.None);

        // Assert
        await _s3Service.DidNotReceive().DownloadProblemFromS3Async(Arg.Any<string>(), Arg.Any<string>());
    }

    // ─────────────────────────────────────────────────
    // SubmitCode — error handling
    // ─────────────────────────────────────────────────

    [Fact]
    public async Task SubmitCode_JudgeThrows_ReturnsNullAndReturnsContainer()
    {
        // Arrange
        var request = CreateSubmissionRequest();
        _fileService.FolderExists(Arg.Any<string>()).Returns(true);

        var callCount = 0;
        _dockerPool.ExecCmdFromContainer(FakeContainerId, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                callCount++;
                if (callCount == 2) throw new Exception("Timeout");
                return Task.FromResult(string.Empty);
            });

        // Act
        var result = await _sut.SubmitCode(request, CancellationToken.None);

        // Assert
        Assert.Null(result);
        await _dockerPool.Received(1).ReturnContainerAsync(FakeContainerId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SubmitCode_InvalidJudgeJson_ReturnsNull()
    {
        // Arrange
        var request = CreateSubmissionRequest();
        _fileService.FolderExists(Arg.Any<string>()).Returns(true);

        _dockerPool.ExecCmdFromContainer(FakeContainerId, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(
                Task.FromResult(string.Empty),
                Task.FromResult("not valid json {{{"),
                Task.FromResult(string.Empty));

        // Act
        var result = await _sut.SubmitCode(request, CancellationToken.None);

        // Assert — JsonSerializer.Deserialize throws, caught by the outer catch
        Assert.Null(result);
    }

    // ─────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────

    private static TestCasePlan CreateTestCasePlan() => new()
    {
        PlanId = "plan-123",
        Version = 1,
        InputCode = "import random\nfor _ in range(5):\n    print(random.randint(1,100))\n    print('---TEST_BOUNDARY---')",
        OutPutCode = "x = int(input())\nprint(x * 2)"
    };

    private static SubmissionRequest CreateSubmissionRequest() => new()
    {
        Id = "sub-1",
        Source = "print('hello')",
        Language = Language.py,
        Problem = new Problem
        {
            Id = "prob-1",
            Version = 0,
            Time = 2,
            Memory = 256
        }
    };
}