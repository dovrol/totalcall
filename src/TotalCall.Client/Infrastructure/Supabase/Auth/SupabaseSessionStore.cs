using System.Globalization;
using System.Text.Json;
using TotalCall.Client.Application.Auth;
using TotalCall.Client.Infrastructure.Browser;

namespace TotalCall.Client.Infrastructure.Supabase.Auth;

/// <summary>
/// Persists the Supabase auth session in <c>localStorage</c> behind a dedicated key,
/// kept separate from the prediction/preference storage. Also holds the short-lived
/// PKCE state that must survive the round trip through the magic-link email.
/// </summary>
public sealed class SupabaseSessionStore(BrowserLocalStorage localStorage)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<AuthSession?> LoadSessionAsync(CancellationToken cancellationToken = default)
    {
        var raw = await localStorage.GetItemAsync(LocalStorageKeys.AuthSession, cancellationToken);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<AuthSession>(raw, JsonOptions);
        }
        catch (JsonException)
        {
            // Corrupt/legacy payload — drop it so the user simply re-authenticates.
            await ClearSessionAsync(cancellationToken);
            return null;
        }
    }

    public ValueTask SaveSessionAsync(AuthSession session, CancellationToken cancellationToken = default)
    {
        var raw = JsonSerializer.Serialize(session, JsonOptions);
        return localStorage.SetItemAsync(LocalStorageKeys.AuthSession, raw, cancellationToken);
    }

    public ValueTask ClearSessionAsync(CancellationToken cancellationToken = default)
    {
        return localStorage.RemoveItemAsync(LocalStorageKeys.AuthSession, cancellationToken);
    }

    public ValueTask SavePkceAsync(PkceState state, CancellationToken cancellationToken = default)
    {
        var raw = JsonSerializer.Serialize(state, JsonOptions);
        return localStorage.SetItemAsync(LocalStorageKeys.AuthPkce, raw, cancellationToken);
    }

    public async Task<PkceState?> LoadPkceAsync(CancellationToken cancellationToken = default)
    {
        var raw = await localStorage.GetItemAsync(LocalStorageKeys.AuthPkce, cancellationToken);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<PkceState>(raw, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public ValueTask ClearPkceAsync(CancellationToken cancellationToken = default)
    {
        return localStorage.RemoveItemAsync(LocalStorageKeys.AuthPkce, cancellationToken);
    }

    public ValueTask SaveLastMagicLinkSentAtAsync(DateTimeOffset sentAt, CancellationToken cancellationToken = default)
    {
        var raw = sentAt.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture);
        return localStorage.SetItemAsync(LocalStorageKeys.AuthLastSent, raw, cancellationToken);
    }

    public async Task<DateTimeOffset?> LoadLastMagicLinkSentAtAsync(CancellationToken cancellationToken = default)
    {
        var raw = await localStorage.GetItemAsync(LocalStorageKeys.AuthLastSent, cancellationToken);
        return long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var unixMs)
            ? DateTimeOffset.FromUnixTimeMilliseconds(unixMs)
            : null;
    }
}

/// <summary>PKCE verifier paired with the path to return to after a successful login.</summary>
public sealed record PkceState(string Verifier, string ReturnUrl);
