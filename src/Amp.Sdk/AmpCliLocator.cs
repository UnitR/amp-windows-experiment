using System.Runtime.InteropServices;

namespace Amp.Sdk;

/// <summary>
/// Finds the Amp CLI the same way the official TypeScript SDK does:
/// <c>AMP_CLI_PATH</c>, then <c>$AMP_HOME/sdk/bin</c>, then <c>$AMP_HOME/bin</c>
/// (<c>AMP_HOME</c> defaults to <c>~/.amp</c>), then every directory on <c>PATH</c>.
/// </summary>
public static class AmpCliLocator
{
    private static readonly bool IsWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    /// <summary>Returns every candidate location in resolution order, whether or not it exists.</summary>
    public static IEnumerable<string> CandidatePaths(Func<string, string?>? getEnv = null)
    {
        getEnv ??= Environment.GetEnvironmentVariable;

        var explicitPath = getEnv("AMP_CLI_PATH");
        if (!string.IsNullOrWhiteSpace(explicitPath))
            yield return explicitPath;

        var home = ResolveAmpHome(getEnv);
        foreach (var dir in new[] { Path.Combine(home, "sdk", "bin"), Path.Combine(home, "bin") })
            foreach (var name in CandidateNames)
                yield return Path.Combine(dir, name);

        var path = getEnv("PATH");
        if (string.IsNullOrEmpty(path)) yield break;
        foreach (var entry in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            foreach (var name in CandidateNames)
                yield return Path.Combine(entry, name);
    }

    /// <summary>Returns the first candidate that exists on disk, or <c>null</c>.</summary>
    public static AmpCommand? Find(Func<string, string?>? getEnv = null)
    {
        foreach (var candidate in CandidatePaths(getEnv))
        {
            if (File.Exists(candidate))
                return new AmpCommand(candidate);
        }
        return null;
    }

    public static string ResolveAmpHome(Func<string, string?>? getEnv = null)
    {
        getEnv ??= Environment.GetEnvironmentVariable;
        var configured = getEnv("AMP_HOME");
        if (!string.IsNullOrWhiteSpace(configured)) return configured;
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".amp");
    }

    private static IReadOnlyList<string> CandidateNames =>
        IsWindows ? ["amp.exe", "amp.cmd", "amp"] : ["amp"];
}
