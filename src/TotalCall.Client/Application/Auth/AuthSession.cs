namespace TotalCall.Client.Application.Auth;

/// <summary>
/// A Supabase auth session as persisted in the browser. Holds the tokens needed to
/// authenticate API calls plus the expiry used to decide when a refresh is required.
/// </summary>
public sealed record AuthSession
{
    public required string AccessToken { get; init; }

    public required string RefreshToken { get; init; }

    /// <summary>Absolute expiry of <see cref="AccessToken"/> (UTC).</summary>
    public required DateTimeOffset ExpiresAt { get; init; }

    public required AuthUser User { get; init; }

    /// <summary>True once the access token has passed (or is about to pass) its expiry.</summary>
    public bool IsExpired(TimeSpan leeway) => DateTimeOffset.UtcNow >= ExpiresAt - leeway;
}
