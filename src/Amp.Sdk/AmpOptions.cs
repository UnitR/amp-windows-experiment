namespace Amp.Sdk;

public enum AmpExecutor { Local, Orb, Runner }

/// <summary>
/// Options for one <see cref="AmpClient.ExecuteAsync(string, AmpOptions?, CancellationToken)"/> call.
/// Mirrors <c>AmpOptions</c> of the TypeScript SDK; each property maps to one CLI flag.
/// </summary>
public sealed record AmpOptions
{
    /// <summary>Working directory of the local CLI process. Defaults to the current directory.</summary>
    public string? Cwd { get; init; }

    /// <summary>Agent mode: <c>low</c>, <c>medium</c>, <c>high</c>, <c>ultra</c>, or a plugin mode key.</summary>
    public string? Mode { get; init; }

    /// <summary>Reasoning effort: none, minimal, low, medium, high, xhigh, max.</summary>
    public string? Effort { get; init; }

    /// <summary>Leave a new execute thread unarchived after the turn completes.</summary>
    public bool NoArchiveAfterExecute { get; init; }

    /// <summary>private, unlisted, workspace, or group.</summary>
    public string? Visibility { get; init; }

    public string? SettingsFile { get; init; }
    public string? LogLevel { get; init; }
    public string? LogFile { get; init; }

    /// <summary>Path to an MCP config JSON file (local executor only).</summary>
    public string? McpConfigFile { get; init; }

    /// <summary>Extra environment variables for the CLI process.</summary>
    public IReadOnlyDictionary<string, string>? Env { get; init; }

    /// <summary>Continue the most recent thread.</summary>
    public bool ContinueLatest { get; init; }

    /// <summary>Continue the thread with this ID (<c>T-…</c>) or URL.</summary>
    public string? ContinueThread { get; init; }

    public string? Skills { get; init; }
    public IReadOnlyList<string>? Labels { get; init; }

    /// <summary>Include thinking blocks in the stream (<c>--stream-json-thinking</c>).</summary>
    public bool Thinking { get; init; }

    public AmpExecutor Executor { get; init; } = AmpExecutor.Local;
    public string? RunnerId { get; init; }
    public string? RunnerDir { get; init; }

    /// <summary>Amp project for a new orb thread.</summary>
    public string? Project { get; init; }

    /// <summary>Title for a new thread. Not allowed when continuing.</summary>
    public string? Title { get; init; }

    public bool DangerouslyAllowAll { get; init; }

    public bool IsRemote => Executor is AmpExecutor.Orb or AmpExecutor.Runner;
    public bool IsContinuing => ContinueLatest || !string.IsNullOrEmpty(ContinueThread);
}
