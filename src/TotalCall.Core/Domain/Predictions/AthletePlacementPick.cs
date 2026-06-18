namespace TotalCall.Core.Domain.Predictions;

public sealed record AthletePlacementPick
{
    public required int Position { get; init; }

    public required string AthleteId { get; init; }

    public bool IsScored { get; init; } = true;

    public bool IsAutoSeeded { get; init; }

    public decimal? PredictedSquatKg { get; init; }

    public decimal? PredictedBenchKg { get; init; }

    public decimal? PredictedDeadliftKg { get; init; }

    public decimal? PredictedTotalKg { get; init; }
}
