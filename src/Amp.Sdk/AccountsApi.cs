using System.Text.Json;

namespace Amp.Sdk;

public sealed record AmpAccount(string? Email, string? UserId, string? Server, bool Active, JsonElement Raw);

/// <summary>Wraps <c>amp account list --json</c>.</summary>
public static class AccountsApi
{
    public static async Task<IReadOnlyList<AmpAccount>> ListAsync(this AmpClient client, CancellationToken cancellationToken = default)
    {
        var result = await client.RunAsync(["account", "list", "--json"], cancellationToken: cancellationToken).ConfigureAwait(false);
        result.ThrowIfFailed("amp account list");
        return ParseAccounts(result.Stdout);
    }

    internal static IReadOnlyList<AmpAccount> ParseAccounts(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("accounts", out var accounts) || accounts.ValueKind != JsonValueKind.Array) return [];
        return accounts.EnumerateArray().Select(a => new AmpAccount(
            Str(a, "email"),
            Str(a, "userId") ?? Str(a, "user_id") ?? Str(a, "id"),
            Str(a, "server") ?? Str(a, "url"),
            a.TryGetProperty("active", out var active) && active.ValueKind == JsonValueKind.True,
            a.Clone())).ToList();
    }

    private static string? Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
}
