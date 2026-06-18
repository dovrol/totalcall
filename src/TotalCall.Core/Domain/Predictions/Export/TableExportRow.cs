namespace TotalCall.Core.Domain.Predictions.Export;

public sealed record TableExportRow
{
    public string Module { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public int? Rank { get; init; }
    public string Athlete { get; init; } = string.Empty;
    public string Country { get; init; } = string.Empty;
    public decimal? NominatedTotalKg { get; init; }
    public decimal? PredictedTotalKg { get; init; }
    public decimal? SquatKg { get; init; }
    public decimal? BenchKg { get; init; }
    public decimal? DeadliftKg { get; init; }
    public string SectionStatus { get; init; } = string.Empty;
}
