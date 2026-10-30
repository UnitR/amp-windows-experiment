namespace Amp.Sdk.Tests;

public class AmpCliArgumentsTests
{
    [Fact]
    public void NewLocalThreadUsesExecuteStreamJson()
    {
        var args = AmpCliArguments.Build(new AmpOptions());
        Assert.Equal(["--execute", "--stream-json"], args);
    }

    [Fact]
    public void ContinueThreadPrefixesThreadsContinue()
    {
        var args = AmpCliArguments.Build(new AmpOptions { ContinueThread = "T-123", Mode = "low", Effort = "high" });
        Assert.Equal(["threads", "continue", "T-123", "--execute", "--stream-json", "--mode", "low", "--effort", "high"], args);
    }

    [Fact]
    public void ContinueLatestOmitsThreadId()
    {
        var args = AmpCliArguments.Build(new AmpOptions { ContinueLatest = true });
        Assert.Equal(["threads", "continue", "--execute", "--stream-json"], args);
    }

    [Fact]
    public void OrbExecutorWithProjectAndTitle()
    {
        var args = AmpCliArguments.Build(new AmpOptions { Executor = AmpExecutor.Orb, Project = "unitr/pub-manager", Title = "Review" });
        Assert.Equal(["--execute", "--stream-json", "--orb-execute", "--project", "unitr/pub-manager", "--title", "Review"], args);
    }

    [Fact]
    public void RunnerExecutorWithDirectory()
    {
        var args = AmpCliArguments.Build(new AmpOptions { Executor = AmpExecutor.Runner, RunnerId = "my-pc", RunnerDir = @"C:\code\app" });
        Assert.Equal(["--execute", "--stream-json", "--executor", "runner:my-pc", "--runner-dir", @"C:\code\app"], args);
    }

    [Fact]
    public void ThinkingLabelsVisibilityAndNoArchive()
    {
        var args = AmpCliArguments.Build(new AmpOptions
        {
            Thinking = true,
            Labels = ["sdk", "windows"],
            Visibility = "public",
            NoArchiveAfterExecute = true,
        });
        Assert.Equal(["--execute", "--stream-json-thinking", "--no-archive-after-execute", "--visibility", "unlisted", "--label", "sdk", "--label", "windows"], args);
    }

    [Fact]
    public void McpConfigIsDroppedForRemoteExecutors()
    {
        var local = AmpCliArguments.Build(new AmpOptions { McpConfigFile = "mcp.json" });
        var remote = AmpCliArguments.Build(new AmpOptions { McpConfigFile = "mcp.json", Executor = AmpExecutor.Orb });
        Assert.Contains("--mcp-config", local);
        Assert.DoesNotContain("--mcp-config", remote);
    }

    [Fact]
    public void StreamingInputAppendsFlagLocallyAndRejectsRemote()
    {
        var local = AmpCliArguments.Build(new AmpOptions(), streamingInput: true);
        Assert.Equal("--stream-json-input", local[^1]);
        Assert.Throws<ArgumentException>(() => AmpCliArguments.Build(new AmpOptions { Executor = AmpExecutor.Orb }, streamingInput: true));
    }

    [Theory]
    [InlineData("project-without-orb")]
    [InlineData("runner-without-id")]
    [InlineData("runner-id-without-executor")]
    [InlineData("title-while-continuing")]
    public void InvalidCombinationsThrow(string scenario)
    {
        AmpOptions options = scenario switch
        {
            "project-without-orb" => new AmpOptions { Project = "x" },
            "runner-without-id" => new AmpOptions { Executor = AmpExecutor.Runner },
            "runner-id-without-executor" => new AmpOptions { RunnerId = "x" },
            _ => new AmpOptions { Title = "t", ContinueThread = "T-1" },
        };
        Assert.Throws<ArgumentException>(() => AmpCliArguments.Build(options));
    }
}
