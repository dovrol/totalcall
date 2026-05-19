namespace TotalCall.Client.Infrastructure.Browser;

public static class LocalStorageKeys
{
    public const string CulturePreference = "totalcall:culture";

    public const string LanguagePreference = "totalcall:language";

    public const string ThemePreference = "totalcall:theme";

    public static string Predictions(string competitionId)
    {
        return $"totalcall:predictions:{competitionId}";
    }
}
