namespace TotalCall.Client.Scoring;

public sealed record QuestionScoreResult(
    string GroupId,
    string QuestionId,
    decimal Points,
    decimal MaxPoints,
    string? CategoryId = null,
    string? Explanation = null,
    IReadOnlyList<SlotScoreResult>? Slots = null,
    IReadOnlyList<OfficialPlacementRef>? Official = null,
    decimal PlacementPoints = 0m,
    decimal PlacementMax = 0m,
    decimal SetBonus = 0m,
    decimal OrderBonus = 0m);

/// <summary>Per-pick scoring verdict for one slot in a placement question.</summary>
public sealed record SlotScoreResult(int Position, string AthleteId, string Verdict, decimal Points);

/// <summary>A single official placement (athlete + finishing position + actual lifts) for a category.</summary>
public sealed record OfficialPlacementRef(
    int Position,
    string AthleteId,
    decimal? TotalKg = null,
    decimal? SquatKg = null,
    decimal? BenchKg = null,
    decimal? DeadliftKg = null);

/// <summary>Verdict identifiers shared between the scorer, snapshot and results UI.</summary>
public static class SlotVerdict
{
    public const string Exact = "exact";
    public const string Wrong = "wrong";
    public const string Miss = "miss";
    public const string Withdrawn = "withdrawn";
}
