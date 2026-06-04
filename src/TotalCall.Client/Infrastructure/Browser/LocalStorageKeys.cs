namespace TotalCall.Client.Infrastructure.Browser;

public static class LocalStorageKeys
{
    public const string CulturePreference = "totalcall:culture";

    public const string LanguagePreference = "totalcall:language";

    public const string ThemePreference = "totalcall:theme";

    public const string LastSeenAppVersion = "totalcall:lastSeenVersion";

    /// <summary>Persisted Supabase auth session (access/refresh tokens + user).</summary>
    public const string AuthSession = "totalcall:auth:session";

    /// <summary>Transient PKCE verifier + return path, kept only between login and callback.</summary>
    public const string AuthPkce = "totalcall:auth:pkce";

    /// <summary>Unix-ms timestamp of the last magic-link send, backing the resend cooldown.</summary>
    public const string AuthLastSent = "totalcall:auth:last-sent";

    public const string PredictionsPrefix = "totalcall:predictions:";

    public static string Predictions(string competitionId)
    {
        return $"{PredictionsPrefix}{competitionId}";
    }

    public static string AthleteDataSource(string competitionId)
    {
        return $"totalcall:athlete-data-source:{competitionId}";
    }
}
