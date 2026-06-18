using TotalCall.Core.Domain.Predictions;

namespace TotalCall.Core.Scoring;

public sealed record OfficialCompetitionResults
{
    public required string CompetitionId { get; init; }

    public string? ResultsHash { get; init; }

    public DateTimeOffset? ImportedAt { get; init; }

    public IReadOnlyList<OfficialResultGroup> Groups { get; init; } = [];

    public OfficialResultGroup? FindFinalGroup(PredictionGroup group, PredictionQuestion question)
    {
        return Groups.FirstOrDefault(resultGroup =>
            resultGroup.Status == OfficialResultGroupStatus.Final &&
            string.Equals(resultGroup.GroupId, group.Id, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(resultGroup.QuestionId, question.Id, StringComparison.OrdinalIgnoreCase));
    }
}

public sealed record OfficialResultGroup
{
    public required string GroupId { get; init; }

    public required string QuestionId { get; init; }

    public string? CategoryId { get; init; }

    public OfficialResultGroupStatus Status { get; init; } = OfficialResultGroupStatus.Pending;

    public string? ResultHash { get; init; }

    public IReadOnlyList<OfficialAthleteResult> Placements { get; init; } = [];
}

public sealed record OfficialAthleteResult
{
    public required int Position { get; init; }

    public required string AthleteId { get; init; }

    public decimal? SquatKg { get; init; }

    public decimal? BenchKg { get; init; }

    public decimal? DeadliftKg { get; init; }

    public decimal? TotalKg { get; init; }
}

public enum OfficialResultGroupStatus
{
    Pending = 0,
    Final = 1
}
