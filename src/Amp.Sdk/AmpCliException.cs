namespace Amp.Sdk;

public class AmpCliException : Exception
{
    public int? ExitCode { get; }
    public string? Stderr { get; }

    public AmpCliException(string message, Exception? inner = null) : base(message, inner) { }

    public AmpCliException(string message, int exitCode, string stderr) : base(message)
    {
        ExitCode = exitCode;
        Stderr = stderr;
    }
}

/// <summary>Thrown when no Amp CLI can be found.</summary>
public sealed class AmpCliNotFoundException(IEnumerable<string> searched)
    : AmpCliException("Amp CLI not found. Install it from https://ampcode.com/install or set AMP_CLI_PATH.")
{
    public IReadOnlyList<string> SearchedPaths { get; } = searched.ToList();
}
