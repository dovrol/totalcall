using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using TotalCall.Client.Domain.Athletes;
using TotalCall.Client.Infrastructure.Json;

namespace TotalCall.Client.Application.Services;

public sealed class AthleteHistoryService(HttpClient httpClient)
{
    private readonly Dictionary<string, AthleteHistoryDataset?> cacheByCompetitionId =
        new(StringComparer.OrdinalIgnoreCase);

    public async Task<AthleteHistoryDataset?> GetCompetitionHistoryAsync(
        string competitionId,
        CancellationToken cancellationToken = default)
    {
        if (cacheByCompetitionId.TryGetValue(competitionId, out var cached))
        {
            return cached;
        }

        try
        {
            var dataset = await httpClient.GetFromJsonAsync<AthleteHistoryDataset>(
                JsonDataPaths.AthleteHistory(competitionId),
                JsonDataOptions.SerializerOptions,
                cancellationToken);

            cacheByCompetitionId[competitionId] = dataset;
            return dataset;
        }
        catch (HttpRequestException exception) when (exception.StatusCode is HttpStatusCode.NotFound)
        {
            cacheByCompetitionId[competitionId] = null;
            return null;
        }
        catch (JsonException)
        {
            cacheByCompetitionId[competitionId] = null;
            return null;
        }
    }
}
