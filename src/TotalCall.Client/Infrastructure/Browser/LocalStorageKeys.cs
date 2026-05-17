namespace TotalCall.Client.Infrastructure.Browser;

public static class LocalStorageKeys
{
    public static string Predictions(string competitionId)
    {
        return $"totalcall:predictions:{competitionId}";
    }
}
