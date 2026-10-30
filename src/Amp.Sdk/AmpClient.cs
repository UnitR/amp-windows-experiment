using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace Amp.Sdk;

/// <summary>
/// Drives the Amp CLI. <see cref="ExecuteAsync(string, AmpOptions?, CancellationToken)"/> is the equivalent of the
/// TypeScript SDK's <c>execute()</c>; <see cref="Threads"/> wraps the <c>amp threads</c> commands.
/// </summary>
public sealed class AmpClient
{
    private readonly AmpCommand? _command;

    /// <param name="command">The CLI to run. When <c>null</c>, it is located with <see cref="AmpCliLocator"/> on first use.</param>
    public AmpClient(AmpCommand? command = null)
    {
        _command = command;
        Threads = new ThreadsApi(this);
    }

    public ThreadsApi Threads { get; }

    public AmpCommand Command => _command ?? AmpCliLocator.Find() ?? throw new AmpCliNotFoundException(AmpCliLocator.CandidatePaths());

    /// <summary>Runs one turn with a single prompt and streams the CLI's messages until the process exits.</summary>
    public IAsyncEnumerable<StreamMessage> ExecuteAsync(string prompt, AmpOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(prompt);
        options ??= new AmpOptions();
        var args = AmpCliArguments.Build(options, streamingInput: false);
        return StreamAsync(args, options, writeInput: async (stdin, ct) =>
        {
            await stdin.WriteAsync((prompt + "\n").AsMemory(), ct).ConfigureAwait(false);
        }, cancellationToken);
    }

    /// <summary>
    /// Runs a multi-turn session with streaming input: each <see cref="UserInputMessage"/> starts a turn, and the process
    /// exits when the input sequence completes. Local executor only.
    /// </summary>
    public IAsyncEnumerable<StreamMessage> ExecuteAsync(IAsyncEnumerable<UserInputMessage> prompts, AmpOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(prompts);
        options ??= new AmpOptions();
        var args = AmpCliArguments.Build(options, streamingInput: true);
        return StreamAsync(args, options, writeInput: async (stdin, ct) =>
        {
            await foreach (var message in prompts.WithCancellation(ct).ConfigureAwait(false))
            {
                await stdin.WriteAsync(message.ToJsonLine().AsMemory(), ct).ConfigureAwait(false);
                await stdin.FlushAsync(ct).ConfigureAwait(false);
            }
        }, cancellationToken);
    }

    private async IAsyncEnumerable<StreamMessage> StreamAsync(
        IReadOnlyList<string> args,
        AmpOptions options,
        Func<StreamWriter, CancellationToken, Task> writeInput,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var process = Start(args, options.Cwd, options.Env);
        var stderr = ReadAllAsync(process.StandardError);

        var inputTask = Task.Run(async () =>
        {
            try
            {
                await writeInput(process.StandardInput, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                try { process.StandardInput.Close(); } catch (IOException) { }
            }
        }, cancellationToken);

        try
        {
            while (await process.StandardOutput.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                yield return StreamMessageParser.Parse(line);
            }

            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            await inputTask.ConfigureAwait(false);
            var errorOutput = await stderr.ConfigureAwait(false);
            if (process.ExitCode != 0)
            {
                var details = string.IsNullOrWhiteSpace(errorOutput) ? "" : $": {errorOutput.Trim()}";
                throw new AmpCliException($"Amp CLI process exited with code {process.ExitCode}{details}", process.ExitCode, errorOutput);
            }
        }
        finally
        {
            Kill(process);
        }
    }

    /// <summary>Runs an arbitrary <c>amp</c> subcommand to completion and returns its output.</summary>
    public async Task<CommandResult> RunAsync(IReadOnlyList<string> args, string? cwd = null, IReadOnlyDictionary<string, string>? env = null, CancellationToken cancellationToken = default)
    {
        using var process = Start(args, cwd, env);
        process.StandardInput.Close();
        var stdout = ReadAllAsync(process.StandardOutput);
        var stderr = ReadAllAsync(process.StandardError);
        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            Kill(process);
        }
        return new CommandResult(process.ExitCode, await stdout.ConfigureAwait(false), await stderr.ConfigureAwait(false));
    }

    /// <summary>Runs <c>amp --version</c>.</summary>
    public async Task<string> GetVersionAsync(CancellationToken cancellationToken = default)
    {
        var result = await RunAsync(["--version"], cancellationToken: cancellationToken).ConfigureAwait(false);
        result.ThrowIfFailed("amp --version");
        return result.Stdout.Trim();
    }

    private Process Start(IReadOnlyList<string> args, string? cwd, IReadOnlyDictionary<string, string>? env)
    {
        var command = Command;
        var startInfo = new ProcessStartInfo
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = new UTF8Encoding(false),
            StandardErrorEncoding = new UTF8Encoding(false),
            StandardInputEncoding = new UTF8Encoding(false),
            WorkingDirectory = cwd ?? Environment.CurrentDirectory,
        };

        var allArgs = command.Args.Concat(args);
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && IsBatchFile(command.Command))
        {
            // CreateProcess cannot start .cmd/.bat files directly.
            startInfo.FileName = Path.Combine(Environment.SystemDirectory, "cmd.exe");
            startInfo.ArgumentList.Add("/d");
            startInfo.ArgumentList.Add("/c");
            startInfo.ArgumentList.Add(command.Command);
        }
        else
        {
            startInfo.FileName = command.Command;
        }
        foreach (var arg in allArgs) startInfo.ArgumentList.Add(arg);

        if (env is not null)
            foreach (var (key, value) in env)
                startInfo.Environment[key] = value;

        try
        {
            return Process.Start(startInfo) ?? throw new AmpCliException($"Failed to start {command}");
        }
        catch (Exception e) when (e is not AmpCliException)
        {
            throw new AmpCliException($"Failed to start {command}: {e.Message}", e);
        }
    }

    private static bool IsBatchFile(string path) =>
        path.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".bat", StringComparison.OrdinalIgnoreCase);

    private static async Task<string> ReadAllAsync(StreamReader reader) =>
        await reader.ReadToEndAsync().ConfigureAwait(false);

    private static void Kill(Process process)
    {
        try
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException) { }
        catch (System.ComponentModel.Win32Exception) { }
    }
}

public sealed record CommandResult(int ExitCode, string Stdout, string Stderr)
{
    public bool Succeeded => ExitCode == 0;

    public void ThrowIfFailed(string what)
    {
        if (ExitCode == 0) return;
        var details = string.IsNullOrWhiteSpace(Stderr) ? "" : $": {Stderr.Trim()}";
        throw new AmpCliException($"{what} exited with code {ExitCode}{details}", ExitCode, Stderr);
    }
}
