using TotalCall.Core.Domain.Competitions;

namespace TotalCall.Client.Application.Providers;

public interface ICompetitionProvider
{
    Task<IReadOnlyList<CompetitionSummary>> GetCompetitionSummariesAsync(
        CancellationToken cancellationToken = default);

    Task<Competition?> GetCompetitionAsync(
        string slug,
        CancellationToken cancellationToken = default);
}
