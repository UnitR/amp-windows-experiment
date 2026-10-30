using System.Text.Json;

namespace Amp.Sdk;

/// <summary>One row of <c>amp threads list --json</c>.</summary>
public sealed record ThreadSummary(string Id, string Title, DateTimeOffset? Updated, string? Tree, int MessageCount)
{
    public string Url => $"https://ampcode.com/threads/{Id}";
}

/// <summary>Wraps the <c>amp threads</c> subcommands (TypeScript SDK's <c>threads</c> namespace, plus <c>list</c>).</summary>
public sealed class ThreadsApi
{
    private readonly AmpClient _client;

    internal ThreadsApi(AmpClient client) => _client = client;

    /// <summary>Creates an empty thread and returns its ID.</summary>
    public async Task<string> NewAsync(string? visibility = null, CancellationToken cancellationToken = default)
    {
        var args = new List<string> { "threads", "new" };
        if (!string.IsNullOrEmpty(visibility)) args.AddRange(["--visibility", visibility]);
        var result = await _client.RunAsync(args, cancellationToken: cancellationToken).ConfigureAwait(false);
        result.ThrowIfFailed("amp threads new");
        return result.Stdout.Trim();
    }

    /// <summary>Returns the thread rendered as Markdown.</summary>
    public async Task<string> MarkdownAsync(string threadId, CancellationToken cancellationToken = default)
    {
        var result = await _client.RunAsync(["threads", "markdown", threadId], cancellationToken: cancellationToken).ConfigureAwait(false);
        result.ThrowIfFailed("amp threads markdown");
        return result.Stdout;
    }

    /// <summary>Turns multiplayer on (optionally for a duration) or off.</summary>
    public async Task SetMultiplayerAsync(string threadId, bool enabled = true, TimeSpan? duration = null, CancellationToken cancellationToken = default)
    {
        var args = new List<string> { "threads", "share", "multiplayer" };
        if (!enabled)
        {
            if (duration is not null) throw new ArgumentException("A duration is not allowed when disabling multiplayer.", nameof(duration));
            args.Add("off");
        }
        else if (duration is { } d)
        {
            args.AddRange(["ttl", $"{(long)Math.Ceiling(d.TotalMinutes)}m"]);
        }
        else
        {
            args.Add("on");
        }
        args.Add(threadId);
        var result = await _client.RunAsync(args, cancellationToken: cancellationToken).ConfigureAwait(false);
        result.ThrowIfFailed("amp threads share multiplayer");
    }

    /// <summary>Lists the signed-in user's threads, newest first.</summary>
    public async Task<IReadOnlyList<ThreadSummary>> ListAsync(int? limit = null, int? offset = null, bool includeArchived = false, CancellationToken cancellationToken = default)
    {
        var args = new List<string> { "threads", "list", "--json" };
        if (limit is { } l) args.AddRange(["--limit", l.ToString()]);
        if (offset is { } o) args.AddRange(["--offset", o.ToString()]);
        if (includeArchived) args.Add("--include-archived");
        var result = await _client.RunAsync(args, cancellationToken: cancellationToken).ConfigureAwait(false);
        result.ThrowIfFailed("amp threads list");
        return ParseList(result.Stdout);
    }

    internal static IReadOnlyList<ThreadSummary> ParseList(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Array) return [];
        var list = new List<ThreadSummary>();
        foreach (var item in root.EnumerateArray())
        {
            var id = item.TryGetProperty("id", out var idProp) ? idProp.GetString() : null;
            if (string.IsNullOrEmpty(id)) continue;
            DateTimeOffset? updated = item.TryGetProperty("updated", out var u) && u.ValueKind == JsonValueKind.String && DateTimeOffset.TryParse(u.GetString(), null, System.Globalization.DateTimeStyles.RoundtripKind, out var parsed)
                ? parsed : null;
            list.Add(new ThreadSummary(
                id,
                item.TryGetProperty("title", out var t) && t.ValueKind == JsonValueKind.String ? t.GetString()! : "",
                updated,
                item.TryGetProperty("tree", out var tree) && tree.ValueKind == JsonValueKind.String ? tree.GetString() : null,
                item.TryGetProperty("messageCount", out var mc) && mc.ValueKind == JsonValueKind.Number ? mc.GetInt32() : 0));
        }
        return list;
    }
}
