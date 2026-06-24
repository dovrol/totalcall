using TotalCall.Client.Application.Providers;
using TotalCall.Core.Domain.Competitions;

namespace TotalCall.Client.Application.Services;

public sealed class CompetitionService(ICompetitionProvider competitionProvider)
{
    public async Task<IReadOnlyList<CompetitionSummary>> GetCompetitionsAsync(
        CancellationToken cancellationToken = default)
    {
        var competitions = await competitionProvider.GetCompetitionSummariesAsync(cancellationToken);

        return competitions
            .OrderByDescending(competition => competition.StartDate)
            .Select(ApplyCurrentStatus)
            .ToArray();
    }

    public async Task<Competition?> GetCompetitionAsync(
        string slug,
        CancellationToken cancellationToken = default)
    {
        var competition = await competitionProvider.GetCompetitionAsync(slug, cancellationToken);

        return competition is null
            ? null
            : competition with { Status = ResolveStatus(competition.Status, competition.PredictionLockAt, competition.EndDate) };
    }

    public bool CanEditPredictions(Competition competition)
    {
        return ResolveStatus(competition.Status, competition.PredictionLockAt, competition.EndDate) == CompetitionStatus.Upcoming;
    }

    private static CompetitionSummary ApplyCurrentStatus(CompetitionSummary competition)
    {
        return competition with
        {
            Status = ResolveStatus(competition.Status, competition.PredictionLockAt, competition.EndDate)
        };
    }

    private static CompetitionStatus ResolveStatus(
        CompetitionStatus configuredStatus,
        DateTimeOffset? predictionLockAt,
        DateTimeOffset? endDate)
    {
        if (configuredStatus is CompetitionStatus.Completed or CompetitionStatus.Archived)
        {
            return configuredStatus;
        }

        var now = DateTimeOffset.UtcNow;

        if (endDate is not null && now > endDate)
        {
            return CompetitionStatus.Completed;
        }

        if (predictionLockAt is not null && now > predictionLockAt)
        {
            return CompetitionStatus.Locked;
        }

        return configuredStatus;
    }
}
