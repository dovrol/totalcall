namespace TotalCall.Core.Domain.Athletes;

public sealed record AthleteHistoryEntry
{
    public string? DisplayName { get; init; }

    public string? CountryCode { get; init; }

    public string? SourceCode { get; init; }

    public IReadOnlyList<AthleteRecentResult> RecentResults { get; init; } = [];

    public AthleteLiftBests? Bests { get; init; }

    public AthleteLastResult? LastResult { get; init; }

    public AthleteAnalytics? Analytics { get; init; }
}

public sealed record AthleteRecentResult
{
    public string? Date { get; init; }

    public string? MeetName { get; init; }

    public string? Federation { get; init; }

    public string? Equipment { get; init; }

    public string? Event { get; init; }

    public decimal? BodyweightKg { get; init; }

    public decimal? SquatKg { get; init; }

    public decimal? BenchKg { get; init; }

    public decimal? DeadliftKg { get; init; }

    public decimal? TotalKg { get; init; }

    public decimal? DotsPoints { get; init; }

    public decimal? GoodliftPoints { get; init; }

    public string? Placing { get; init; }
}

public sealed record AthleteLiftBests
{
    public decimal? SquatKg { get; init; }

    public decimal? BenchKg { get; init; }

    public decimal? DeadliftKg { get; init; }

    public decimal? TotalKg { get; init; }
}

public sealed record AthleteLastResult
{
    public string? Date { get; init; }

    public string? MeetName { get; init; }

    public decimal? SquatKg { get; init; }

    public decimal? BenchKg { get; init; }

    public decimal? DeadliftKg { get; init; }

    public decimal? TotalKg { get; init; }

    public decimal? BodyweightKg { get; init; }
}

public sealed record AthleteAnalytics
{
    public int StartsCount { get; init; }

    public int? FirstStartYear { get; init; }

    public int? LastStartYear { get; init; }

    public decimal? BestTotalKg { get; init; }

    public decimal? LastTotalKg { get; init; }

    public decimal? LastTotalToBestPercent { get; init; }

    public decimal? Best12MonthTotalKg { get; init; }

    public decimal? Last3AvgTotalKg { get; init; }

    public decimal? Last5AvgTotalKg { get; init; }

    public decimal? TotalTrendKg { get; init; }

    public decimal? RecentTotalTrendKg { get; init; }

    public int? RecentTotalTrendStarts { get; init; }

    public decimal? TotalStabilityKg { get; init; }

    public int TotalStabilityStarts { get; init; }

    public int TotalMetricStartsCount { get; init; }

    public decimal? BestSquatKg { get; init; }

    public decimal? BestBenchKg { get; init; }

    public decimal? BestDeadliftKg { get; init; }

    public decimal? BestDotsPoints { get; init; }

    public decimal? BestGoodliftPoints { get; init; }

    public AthleteAttemptSuccessRate SquatAttempts { get; init; } = new();

    public AthleteAttemptSuccessRate BenchAttempts { get; init; } = new();

    public AthleteAttemptSuccessRate DeadliftAttempts { get; init; } = new();

    public AthleteAttemptSuccessRate OverallAttempts { get; init; } = new();

    public AthleteAttemptSuccessRate ThirdAttempts { get; init; } = new();
}

public sealed record AthleteAttemptSuccessRate
{
    public decimal? RatePercent { get; init; }

    public int SuccessfulAttempts { get; init; }

    public int CountedAttempts { get; init; }
}

public enum AthleteAttemptBenchmarkScope
{
    Category,
    Sex,
    Field
}

public sealed record AthleteAttemptBenchmark
{
    public AthleteAttemptBenchmarkScope Scope { get; init; }

    public AthleteSex Sex { get; init; } = AthleteSex.Unspecified;

    public int ComparedAthleteCount { get; init; }

    public AthleteAttemptSuccessRate SquatAttempts { get; init; } = new();

    public AthleteAttemptSuccessRate BenchAttempts { get; init; } = new();

    public AthleteAttemptSuccessRate DeadliftAttempts { get; init; } = new();

    public AthleteAttemptSuccessRate OverallAttempts { get; init; } = new();

    public AthleteAttemptSuccessRate ThirdAttempts { get; init; } = new();
}
