using Amp.Sdk;

namespace Amp.App.Services;

/// <summary>Builds <see cref="AmpClient"/> instances from the current settings and reports CLI / sign-in state.</summary>
public sealed class AmpService(AppSettings settings)
{
    public AmpCommand? ResolveCommand() =>
        !string.IsNullOrWhiteSpace(settings.CliPath)
            ? (File.Exists(settings.CliPath) ? new AmpCommand(settings.CliPath) : null)
            : AmpCliLocator.Find();

    public IReadOnlyList<string> SearchedPaths => AmpCliLocator.CandidatePaths().ToList();

    public AmpClient CreateClient()
    {
        var command = ResolveCommand() ?? throw new AmpCliNotFoundException(SearchedPaths);
        return new AmpClient(command);
    }

    /// <summary>Environment for CLI processes: the stored API key, when one is set.</summary>
    public IReadOnlyDictionary<string, string>? Environment()
    {
        var key = settings.ApiKey;
        return string.IsNullOrEmpty(key) ? null : new Dictionary<string, string> { ["AMP_API_KEY"] = key };
    }

    public async Task<CliStatus> CheckAsync(CancellationToken cancellationToken = default)
    {
        var command = ResolveCommand();
        if (command is null) return new CliStatus(null, null, false, null);

        var client = new AmpClient(command);
        string? version = null;
        try
        {
            version = await client.GetVersionAsync(cancellationToken);
        }
        catch (AmpCliException e)
        {
            return new CliStatus(command.Command, null, false, e.Message);
        }

        if (!string.IsNullOrEmpty(settings.ApiKey)) return new CliStatus(command.Command, version, true, null);

        try
        {
            var accounts = await client.ListAsync(cancellationToken);
            return new CliStatus(command.Command, version, accounts.Count > 0, null);
        }
        catch (AmpCliException e)
        {
            return new CliStatus(command.Command, version, false, e.Message);
        }
    }
}

public sealed record CliStatus(string? Path, string? Version, bool SignedIn, string? Error)
{
    public bool Found => Path is not null;
}
