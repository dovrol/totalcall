namespace TotalCall.Client.Application.Windows;

public sealed class AthleteComparisonWindowDescriptor : FloatingWindowDescriptor
{
    public string CompetitionId { get; }

    public string ContextId { get; }

    public string? CategoryName { get; private set; }

    public int ScoredCount { get; private set; }

    public IReadOnlyList<AthleteComparisonWindowSlot> Slots { get; private set; }

    public AthleteComparisonWindowDescriptor(
        string competitionId,
        string contextId,
        string? categoryName,
        int scoredCount,
        IReadOnlyList<AthleteComparisonWindowSlot> slots,
        WindowPosition position,
        string title,
        string? subtitle)
        : base(WindowKind, title, subtitle, position)
    {
        CompetitionId = competitionId;
        ContextId = contextId;
        CategoryName = categoryName;
        ScoredCount = scoredCount;
        Slots = slots;
    }

    public void Update(
        string? categoryName,
        int scoredCount,
        IReadOnlyList<AthleteComparisonWindowSlot> slots,
        string title,
        string? subtitle)
    {
        CategoryName = categoryName;
        ScoredCount = scoredCount;
        Slots = slots;
        Title = title;
        Subtitle = subtitle;
    }

    public const string WindowKind = "athlete-comparison";
}

public sealed record AthleteComparisonWindowSlot
{
    public required int Position { get; init; }

    public required string AthleteId { get; init; }

    public required string AthleteName { get; init; }

    public string? CountryCode { get; init; }

    public string? CountryName { get; init; }

    public decimal? NominatedTotalKg { get; init; }

    public decimal? PredictedTotalKg { get; init; }

    public decimal? PredictedSquatKg { get; init; }

    public decimal? PredictedBenchKg { get; init; }

    public decimal? PredictedDeadliftKg { get; init; }

    public bool IsAutoSeeded { get; init; }
}
