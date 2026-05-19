using System.Net.Http.Json;
using TotalCall.Client.Application.Providers;
using TotalCall.Client.Domain.Competitions;

namespace TotalCall.Client.Infrastructure.Json;

public sealed class JsonCompetitionProvider(HttpClient httpClient) : ICompetitionProvider
{
    private readonly Dictionary<string, Competition> competitionsBySlug = new(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyList<CompetitionSummary>? competitionSummaries;

    public async Task<IReadOnlyList<CompetitionSummary>> GetCompetitionSummariesAsync(
        CancellationToken cancellationToken = default)
    {
        if (competitionSummaries is not null)
        {
            return competitionSummaries;
        }

        var competitions = await httpClient.GetFromJsonAsync<IReadOnlyList<CompetitionSummary>>(
            JsonDataPaths.CompetitionIndex,
            JsonDataOptions.SerializerOptions,
            cancellationToken);

        competitionSummaries = competitions ?? [];

        return competitionSummaries;
    }

    public async Task<Competition?> GetCompetitionAsync(
        string slug,
        CancellationToken cancellationToken = default)
    {
        if (competitionsBySlug.TryGetValue(slug, out var cachedCompetition))
        {
            return cachedCompetition;
        }

        var competition = await httpClient.GetFromJsonAsync<Competition>(
            JsonDataPaths.Competition(slug),
            JsonDataOptions.SerializerOptions,
            cancellationToken);

        if (competition is not null)
        {
            competitionsBySlug[slug] = competition;
        }

        return competition;
    }
}
