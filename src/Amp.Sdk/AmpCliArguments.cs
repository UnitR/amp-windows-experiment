namespace Amp.Sdk;

/// <summary>Builds the <c>amp</c> argument list for an execute call, matching the TypeScript SDK's <c>buildCliArgs</c>.</summary>
public static class AmpCliArguments
{
    public static List<string> Build(AmpOptions options, bool streamingInput = false)
    {
        Validate(options, streamingInput);

        var args = new List<string>();

        if (!string.IsNullOrEmpty(options.ContinueThread))
            args.AddRange(["threads", "continue", options.ContinueThread]);
        else if (options.ContinueLatest)
            args.AddRange(["threads", "continue"]);

        args.Add("--execute");
        args.Add(options.Thinking ? "--stream-json-thinking" : "--stream-json");

        switch (options.Executor)
        {
            case AmpExecutor.Orb:
                args.Add("--orb-execute");
                if (!string.IsNullOrEmpty(options.Project))
                    args.AddRange(["--project", options.Project]);
                break;
            case AmpExecutor.Runner:
                args.AddRange(["--executor", $"runner:{options.RunnerId}"]);
                if (options.RunnerDir is not null)
                    args.AddRange(["--runner-dir", options.RunnerDir]);
                break;
        }

        if (options.DangerouslyAllowAll) args.Add("--dangerously-allow-all");
        if (options.NoArchiveAfterExecute) args.Add("--no-archive-after-execute");

        var visibility = options.Visibility == "public" ? "unlisted" : options.Visibility;
        if (!string.IsNullOrEmpty(visibility)) args.AddRange(["--visibility", visibility]);

        if (!string.IsNullOrEmpty(options.SettingsFile)) args.AddRange(["--settings-file", options.SettingsFile]);
        if (!string.IsNullOrEmpty(options.LogLevel)) args.AddRange(["--log-level", options.LogLevel]);
        if (!string.IsNullOrEmpty(options.LogFile)) args.AddRange(["--log-file", options.LogFile]);
        if (!string.IsNullOrEmpty(options.McpConfigFile) && !options.IsRemote) args.AddRange(["--mcp-config", options.McpConfigFile]);
        if (!string.IsNullOrEmpty(options.Mode)) args.AddRange(["--mode", options.Mode]);
        if (!string.IsNullOrEmpty(options.Effort)) args.AddRange(["--effort", options.Effort]);

        if (options.Labels is not null)
            foreach (var label in options.Labels)
                args.AddRange(["--label", label]);

        if (!string.IsNullOrEmpty(options.Title)) args.AddRange(["--title", options.Title]);

        if (streamingInput) args.Add("--stream-json-input");

        return args;
    }

    private static void Validate(AmpOptions o, bool streamingInput)
    {
        if (o.Project is not null && o.Executor != AmpExecutor.Orb)
            throw new ArgumentException("The Project option requires the Orb executor.", nameof(o));
        if (o.Executor == AmpExecutor.Runner && string.IsNullOrEmpty(o.RunnerId))
            throw new ArgumentException("The Runner executor requires RunnerId.", nameof(o));
        if (o.RunnerId is not null && o.Executor != AmpExecutor.Runner)
            throw new ArgumentException("RunnerId requires the Runner executor.", nameof(o));
        if (o.RunnerDir is not null && o.Executor != AmpExecutor.Runner)
            throw new ArgumentException("RunnerDir requires the Runner executor.", nameof(o));
        if (o.Title is not null && o.IsContinuing)
            throw new ArgumentException("Title is only supported when creating a new thread.", nameof(o));
        if (streamingInput && o.IsRemote)
            throw new ArgumentException($"Streaming input is not supported with the {o.Executor} executor; pass a string prompt instead.", nameof(o));
    }
}
