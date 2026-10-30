namespace Amp.Sdk;

/// <summary>
/// A resolved way to start the Amp CLI: an executable plus any leading arguments.
/// </summary>
public sealed record AmpCommand(string Command, IReadOnlyList<string> Args)
{
    public AmpCommand(string command) : this(command, []) { }

    public override string ToString() => Args.Count == 0 ? Command : $"{Command} {string.Join(' ', Args)}";
}
