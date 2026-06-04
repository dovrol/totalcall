namespace TotalCall.Client.Infrastructure.Supabase.Auth;

/// <summary>
/// Raised when a Supabase Auth (GoTrue) request fails. <see cref="Exception.Message"/>
/// carries a user-presentable description extracted from the GoTrue error payload.
/// </summary>
public sealed class SupabaseAuthException : Exception
{
    public SupabaseAuthException(string message)
        : base(message)
    {
    }

    public SupabaseAuthException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
