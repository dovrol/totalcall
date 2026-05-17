using System.Net.Http.Json;
using TotalCall.Client.Application.Providers;
using TotalCall.Client.Domain.Competitions;

namespace TotalCall.Client.Infrastructure.Json;

public sealed class JsonCompetitionProvider(HttpClient httpClient) : ICompetitionProvider
{
    public async Task<IReadOnlyList<CompetitionSummary>> GetCompetitionSummariesAsync(
        CancellationToken cancellationToken = default)
    {
        var competitions = await httpClient.GetFromJsonAsync<IReadOnlyList<CompetitionSummary>>(
            JsonDataPaths.CompetitionIndex,
            JsonDataOptions.SerializerOptions,
            cancellationToken);

        return competitions ?? [];
    }

    public async Task<Competition?> GetCompetitionAsync(
        string slug,
        CancellationToken cancellationToken = default)
    {
        return await httpClient.GetFromJsonAsync<Competition>(
            JsonDataPaths.Competition(slug),
            JsonDataOptions.SerializerOptions,
            cancellationToken);
    }
}
