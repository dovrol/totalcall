namespace TotalCall.Client.Domain.Predictions.Review;

public sealed record ReviewPickRowModel
{
    public int? Position { get; init; }

    public string AthleteId { get; init; } = string.Empty;

    public string AthleteName { get; init; } = string.Empty;

    public string? CountryCode { get; init; }

    public string? CountryName { get; init; }

    public decimal? PredictedTotalKg { get; init; }

    public decimal? PredictedSquatKg { get; init; }

    public decimal? PredictedBenchKg { get; init; }

    public decimal? PredictedDeadliftKg { get; init; }

    public bool HasAthlete => !string.IsNullOrWhiteSpace(AthleteId);

    public bool HasAnyLift =>
        PredictedSquatKg is not null ||
        PredictedBenchKg is not null ||
        PredictedDeadliftKg is not null;
}
