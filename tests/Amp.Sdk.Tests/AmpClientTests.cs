using System.Runtime.InteropServices;

namespace Amp.Sdk.Tests;

/// <summary>Runs <see cref="AmpClient"/> against a fake <c>amp</c> script that replays the recorded fixture.</summary>
public class AmpClientTests : IDisposable
{
    private static readonly bool IsWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
    private readonly string _dir = Directory.CreateTempSubdirectory("amp-client").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private AmpCommand Fake(string behaviour)
    {
        var fixture = Path.Combine(AppContext.BaseDirectory, "Fixtures", "shell-turn.jsonl");
        var args = Path.Combine(_dir, "args.txt");
        var stdin = Path.Combine(_dir, "stdin.txt");
        string path, script;
        if (IsWindows)
        {
            path = Path.Combine(_dir, "amp.cmd");
            script = behaviour switch
            {
                "replay" => $"@echo off\r\necho %* > \"{args}\"\r\nfindstr /r \"^\" > \"{stdin}\"\r\ntype \"{fixture}\"\r\nexit /b 0\r\n",
                "fail" => "@echo off\r\necho something broke 1>&2\r\nexit /b 3\r\n",
                _ => "@echo off\r\nping -n 31 127.0.0.1 >nul\r\n",
            };
        }
        else
        {
            path = Path.Combine(_dir, "amp");
            script = behaviour switch
            {
                "replay" => $"#!/bin/sh\nprintf '%s\\n' \"$*\" > '{args}'\ncat > '{stdin}'\ncat '{fixture}'\n",
                "fail" => "#!/bin/sh\necho 'something broke' >&2\nexit 3\n",
                _ => "#!/bin/sh\nsleep 30\n",
            };
        }
        File.WriteAllText(path, script);
        if (!IsWindows) File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        return new AmpCommand(path);
    }

    [Fact]
    public async Task ExecuteStreamsMessagesPassesArgsAndWritesPrompt()
    {
        var client = new AmpClient(Fake("replay"));
        var messages = new List<StreamMessage>();
        await foreach (var m in client.ExecuteAsync("say done", new AmpOptions { Mode = "low", Cwd = _dir }))
            messages.Add(m);

        Assert.Equal(6, messages.Count);
        Assert.IsType<SystemMessage>(messages[0]);
        var result = Assert.IsType<ResultMessage>(messages[^1]);
        Assert.Equal("done", result.Result);

        var recordedArgs = (await File.ReadAllTextAsync(Path.Combine(_dir, "args.txt"))).Trim();
        Assert.Contains("--execute", recordedArgs);
        Assert.Contains("--stream-json", recordedArgs);
        Assert.Contains("--mode low", recordedArgs);
        Assert.Equal("say done", (await File.ReadAllTextAsync(Path.Combine(_dir, "stdin.txt"))).Trim());
    }

    [Fact]
    public async Task StreamingInputWritesJsonLines()
    {
        var client = new AmpClient(Fake("replay"));
        async IAsyncEnumerable<UserInputMessage> Prompts()
        {
            yield return new UserInputMessage("first", "r1");
            await Task.Yield();
            yield return new UserInputMessage("second");
        }

        var count = 0;
        await foreach (var _ in client.ExecuteAsync(Prompts())) count++;
        Assert.Equal(6, count);

        var lines = (await File.ReadAllTextAsync(Path.Combine(_dir, "stdin.txt"))).Trim().Split('\n');
        Assert.Equal(2, lines.Length);
        Assert.Contains("\"request_id\":\"r1\"", lines[0]);
        Assert.Contains("\"text\":\"second\"", lines[1]);
        Assert.Contains("--stream-json-input", await File.ReadAllTextAsync(Path.Combine(_dir, "args.txt")));
    }

    [Fact]
    public async Task NonZeroExitSurfacesStderr()
    {
        var client = new AmpClient(Fake("fail"));
        var e = await Assert.ThrowsAsync<AmpCliException>(async () =>
        {
            await foreach (var _ in client.ExecuteAsync("x")) { }
        });
        Assert.Equal(3, e.ExitCode);
        Assert.Contains("something broke", e.Message);
    }

    [Fact]
    public async Task CancellationKillsTheProcessPromptly()
    {
        var client = new AmpClient(Fake("hang"));
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));
        var started = DateTime.UtcNow;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await foreach (var _ in client.ExecuteAsync("x", cancellationToken: cts.Token)) { }
        });
        Assert.True(DateTime.UtcNow - started < TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task RunAsyncReturnsOutputAndExitCode()
    {
        var client = new AmpClient(Fake("fail"));
        var result = await client.RunAsync(["--version"]);
        Assert.Equal(3, result.ExitCode);
        Assert.False(result.Succeeded);
        Assert.Contains("something broke", result.Stderr);
        Assert.Throws<AmpCliException>(() => result.ThrowIfFailed("amp --version"));
    }

    [Fact]
    public void MissingCliThrowsNotFoundWithSearchedPaths()
    {
        var client = new AmpClient(new AmpCommand(Path.Combine(_dir, "nope")));
        Assert.Throws<AmpCliException>(() => client.GetVersionAsync().GetAwaiter().GetResult());
    }
}
