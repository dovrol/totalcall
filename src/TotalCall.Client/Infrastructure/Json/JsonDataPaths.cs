namespace TotalCall.Client.Infrastructure.Json;

public static class JsonDataPaths
{
    public const string CompetitionIndex = "data/competitions/index.json";

    public static string Competition(string slug)
    {
        return $"data/competitions/{slug}.json";
    }
}
