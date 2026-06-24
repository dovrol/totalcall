using TotalCall.Client.Application.Providers;
using TotalCall.Client.Application.Services;
using TotalCall.Core.Domain.Competitions;

namespace TotalCall.Tests.Application.Services;

public sealed class CompetitionServiceTests
{
    [Fact]
    public async Task List_marks_competition_completed_once_end_date_passed()
    {
        // Regression: the list must roll a finished competition into Completed,
        // not leave it stuck on Locked ("Awaiting results").
        var service = BuildService(Summary(
            status: CompetitionStatus.Upcoming,
            predictionLockAt: DateTimeOffset.UtcNow.AddDays(-10),
            endDate: DateTimeOffset.UtcNow.AddDays(-1)));

        var competitions = await service.GetCompetitionsAsync();

        Assert.Equal(CompetitionStatus.Completed, competitions[0].Status);
    }

    [Fact]
    public async Task List_marks_competition_locked_between_lock_and_end()
    {
        var service = BuildService(Summary(
            status: CompetitionStatus.Upcoming,
            predictionLockAt: DateTimeOffset.UtcNow.AddDays(-1),
            endDate: DateTimeOffset.UtcNow.AddDays(5)));

        var competitions = await service.GetCompetitionsAsync();

        Assert.Equal(CompetitionStatus.Locked, competitions[0].Status);
    }

    [Fact]
    public async Task List_keeps_competition_upcoming_before_lock()
    {
        var service = BuildService(Summary(
            status: CompetitionStatus.Upcoming,
            predictionLockAt: DateTimeOffset.UtcNow.AddDays(1),
            endDate: DateTimeOffset.UtcNow.AddDays(5)));

        var competitions = await service.GetCompetitionsAsync();

        Assert.Equal(CompetitionStatus.Upcoming, competitions[0].Status);
    }

    private static CompetitionService BuildService(CompetitionSummary summary) =>
        new(new FakeCompetitionProvider([summary]));

    private static CompetitionSummary Summary(
        CompetitionStatus status,
        DateTimeOffset predictionLockAt,
        DateTimeOffset endDate) => new()
    {
        Id = "worlds-2026",
        Slug = "worlds-2026",
        Name = "Worlds 2026",
        ConfigVersion = "2026.1",
        Status = status,
        StartDate = predictionLockAt.AddHours(1),
        EndDate = endDate,
        PredictionLockAt = predictionLockAt
    };

    private sealed class FakeCompetitionProvider(IReadOnlyList<CompetitionSummary> summaries)
        : ICompetitionProvider
    {
        public Task<IReadOnlyList<CompetitionSummary>> GetCompetitionSummariesAsync(
            CancellationToken cancellationToken = default) => Task.FromResult(summaries);

        public Task<Competition?> GetCompetitionAsync(
            string slug,
            CancellationToken cancellationToken = default) => Task.FromResult<Competition?>(null);
    }
}
