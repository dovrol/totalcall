using Microsoft.AspNetCore.Components;
using TotalCall.Client.Infrastructure.Supabase.Auth;

namespace TotalCall.Client.Application.Auth;

/// <summary>
/// Central authentication entry point for the app. Owns the current session, drives the
/// Supabase magic-link + PKCE flow, persists the session through <see cref="SupabaseSessionStore"/>,
/// and refreshes the access token on demand. UI reacts via <see cref="AuthStateChanged"/>
/// (and, idiomatically, through the <see cref="TotalCallAuthenticationStateProvider"/>).
/// </summary>
public sealed class AuthService(
    SupabaseAuthClient client,
    SupabaseSessionStore store,
    NavigationManager navigation)
{
    private static readonly TimeSpan RefreshLeeway = TimeSpan.FromSeconds(60);
    private const string CallbackPath = "auth/callback";

    /// <summary>Minimum delay between magic-link sends, to stop resend spamming.</summary>
    public static readonly TimeSpan ResendCooldown = TimeSpan.FromSeconds(60);

    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    private AuthSession? _session;
    private DateTimeOffset? _lastMagicLinkSentAt;

    /// <summary>Raised whenever the signed-in state meaningfully changes (login / logout).</summary>
    public event Action? AuthStateChanged;

    public bool IsAuthenticated => _session is not null;

    public AuthUser? CurrentUser => _session?.User;

    /// <summary>The last known session snapshot. Not refreshed — use <see cref="GetSessionAsync"/> for that.</summary>
    public AuthSession? CurrentSession => _session;

    /// <summary>
    /// Restores any persisted session on startup and silently refreshes it when the access
    /// token has expired. Called once during host startup before the first render.
    /// </summary>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        // Restore the resend cooldown first so a page reload can't bypass it.
        _lastMagicLinkSentAt = await store.LoadLastMagicLinkSentAtAsync(cancellationToken);

        var stored = await store.LoadSessionAsync(cancellationToken);

        if (stored is null)
        {
            return;
        }

        if (!stored.IsExpired(RefreshLeeway))
        {
            _session = stored;
        }
        else if (!string.IsNullOrEmpty(stored.RefreshToken))
        {
            try
            {
                var refreshed = await client.RefreshSessionAsync(stored.RefreshToken, cancellationToken);
                await PersistAsync(refreshed, cancellationToken);
            }
            catch (Exception)
            {
                await WipeAsync(cancellationToken);
            }
        }
        else
        {
            await WipeAsync(cancellationToken);
        }

        AuthStateChanged?.Invoke();
    }

    /// <summary>Time left before another magic link may be requested; <see cref="TimeSpan.Zero"/> when ready.</summary>
    public TimeSpan GetResendCooldownRemaining()
    {
        if (_lastMagicLinkSentAt is null)
        {
            return TimeSpan.Zero;
        }

        var remaining = ResendCooldown - (DateTimeOffset.UtcNow - _lastMagicLinkSentAt.Value);
        return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
    }

    /// <summary>
    /// Starts the magic-link flow: generates a PKCE pair, stashes the verifier for the
    /// callback, and asks Supabase to email a link that returns to <c>/auth/callback</c>.
    /// Honours the resend cooldown — throws if called again too soon.
    /// </summary>
    public async Task SendMagicLinkAsync(
        string email,
        string? returnUrl = null,
        CancellationToken cancellationToken = default)
    {
        if (GetResendCooldownRemaining() > TimeSpan.Zero)
        {
            throw new SupabaseAuthException("A magic link was just sent. Please wait before requesting another.");
        }

        var normalizedEmail = email.Trim();
        var verifier = Pkce.CreateVerifier();
        var challenge = Pkce.CreateChallenge(verifier);

        await store.SavePkceAsync(new PkceState(verifier, NormalizeReturnUrl(returnUrl)), cancellationToken);
        await client.SendMagicLinkAsync(normalizedEmail, BuildRedirectUrl(), challenge, cancellationToken);

        // Start the cooldown only after a successful send.
        _lastMagicLinkSentAt = DateTimeOffset.UtcNow;
        await store.SaveLastMagicLinkSentAtAsync(_lastMagicLinkSentAt.Value, cancellationToken);
    }

    /// <summary>
    /// Completes the magic-link round trip from <c>/auth/callback</c>: reads the auth code from
    /// the URL, exchanges it (with the stored verifier) for a session, and persists it.
    /// Returns the relative path to navigate to next. Throws on a failed or malformed callback.
    /// </summary>
    public async Task<string> HandleCallbackAsync(CancellationToken cancellationToken = default)
    {
        var parameters = ReadCallbackParameters();

        if (parameters.TryGetValue("error_description", out var description) && !string.IsNullOrWhiteSpace(description))
        {
            await store.ClearPkceAsync(cancellationToken);
            throw new SupabaseAuthException(description);
        }

        if (parameters.TryGetValue("error", out var error) && !string.IsNullOrWhiteSpace(error))
        {
            await store.ClearPkceAsync(cancellationToken);
            throw new SupabaseAuthException(error);
        }

        if (!parameters.TryGetValue("code", out var code) || string.IsNullOrWhiteSpace(code))
        {
            throw new SupabaseAuthException("Missing authentication code in the callback URL.");
        }

        var pkce = await store.LoadPkceAsync(cancellationToken)
            ?? throw new SupabaseAuthException("Login session expired. Please request a new magic link.");

        try
        {
            var session = await client.ExchangeCodeForSessionAsync(code, pkce.Verifier, cancellationToken);
            await PersistAsync(session, cancellationToken);
            AuthStateChanged?.Invoke();
            return pkce.ReturnUrl;
        }
        finally
        {
            await store.ClearPkceAsync(cancellationToken);
        }
    }

    /// <summary>Returns the current session, refreshing the access token first if it has expired.</summary>
    public Task<AuthSession?> GetSessionAsync(CancellationToken cancellationToken = default)
    {
        return EnsureValidSessionAsync(cancellationToken);
    }

    /// <summary>
    /// Returns a valid access token for authenticating API calls (refreshing if needed),
    /// or <c>null</c> when the user is signed out.
    /// </summary>
    public async Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        var session = await EnsureValidSessionAsync(cancellationToken);
        return session?.AccessToken;
    }

    /// <summary>Revokes the session server-side (best effort) and clears it locally.</summary>
    public async Task SignOutAsync(CancellationToken cancellationToken = default)
    {
        var current = _session;

        if (current is not null)
        {
            try
            {
                await client.SignOutAsync(current.AccessToken, cancellationToken);
            }
            catch (Exception)
            {
                // The token may already be invalid — clearing locally is what matters.
            }
        }

        await WipeAsync(cancellationToken);
        AuthStateChanged?.Invoke();
    }

    private async Task<AuthSession?> EnsureValidSessionAsync(CancellationToken cancellationToken)
    {
        var current = _session;

        if (current is null || !current.IsExpired(RefreshLeeway))
        {
            return current;
        }

        if (string.IsNullOrEmpty(current.RefreshToken))
        {
            await WipeAsync(cancellationToken);
            AuthStateChanged?.Invoke();
            return null;
        }

        await _refreshGate.WaitAsync(cancellationToken);
        try
        {
            current = _session;

            if (current is null || !current.IsExpired(RefreshLeeway))
            {
                return current;
            }

            var refreshed = await client.RefreshSessionAsync(current.RefreshToken, cancellationToken);
            await PersistAsync(refreshed, cancellationToken);
            return refreshed;
        }
        catch (Exception)
        {
            await WipeAsync(cancellationToken);
            AuthStateChanged?.Invoke();
            return null;
        }
        finally
        {
            _refreshGate.Release();
        }
    }

    private async Task PersistAsync(AuthSession session, CancellationToken cancellationToken)
    {
        _session = session;
        await store.SaveSessionAsync(session, cancellationToken);
    }

    private async Task WipeAsync(CancellationToken cancellationToken)
    {
        _session = null;
        await store.ClearSessionAsync(cancellationToken);
    }

    private string BuildRedirectUrl()
    {
        return new Uri(new Uri(navigation.BaseUri), CallbackPath).ToString();
    }

    private static string NormalizeReturnUrl(string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl))
        {
            return "/";
        }

        var trimmed = returnUrl.Trim();

        // Only allow app-relative paths — reject absolute / protocol-relative URLs (open-redirect guard).
        if (trimmed.StartsWith("//", StringComparison.Ordinal) ||
            trimmed.Contains("://", StringComparison.Ordinal))
        {
            return "/";
        }

        return "/" + trimmed.TrimStart('/');
    }

    private IReadOnlyDictionary<string, string> ReadCallbackParameters()
    {
        var uri = new Uri(navigation.Uri);
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        MergeQuery(values, uri.Query);

        // Defensive: some Supabase error redirects place details in the hash fragment.
        if (uri.Fragment.Length > 1)
        {
            MergeQuery(values, uri.Fragment);
        }

        return values;
    }

    private static void MergeQuery(IDictionary<string, string> values, string query)
    {
        var trimmed = query.TrimStart('?', '#');
        if (trimmed.Length == 0)
        {
            return;
        }

        foreach (var pair in trimmed.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = pair.IndexOf('=');
            if (separator <= 0)
            {
                continue;
            }

            var key = Uri.UnescapeDataString(pair[..separator]);
            var value = Uri.UnescapeDataString(pair[(separator + 1)..]);
            values[key] = value;
        }
    }
}
