namespace TotalCall.Client.Application.Auth;

/// <summary>The authenticated Supabase user, trimmed to what the UI needs.</summary>
public sealed record AuthUser
{
    public required string Id { get; init; }

    public string? Email { get; init; }
}
