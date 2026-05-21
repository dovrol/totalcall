namespace TotalCall.Client.Domain.Athletes;

public sealed record AthleteHistoryDataset
{
    public required string CompetitionId { get; init; }

    public AthleteHistorySource? Source { get; init; }

    public IReadOnlyDictionary<string, AthleteHistoryEntry> Athletes { get; init; } =
        new Dictionary<string, AthleteHistoryEntry>(StringComparer.OrdinalIgnoreCase);
}

public sealed record AthleteHistorySource
{
    public string? Name { get; init; }

    public string? Attribution { get; init; }

    public string? DownloadedAt { get; init; }
}

public sealed record AthleteHistoryEntry
{
    public string? DisplayName { get; init; }

    public string? CountryCode { get; init; }

    public IReadOnlyList<AthleteRecentResult> RecentResults { get; init; } = [];

    public AthleteLiftBests? Bests { get; init; }

    public AthleteLastResult? LastResult { get; init; }
}

public sealed record AthleteRecentResult
{
    public string? Date { get; init; }

    public string? MeetName { get; init; }

    public string? Federation { get; init; }

    public string? Equipment { get; init; }

    public decimal? BodyweightKg { get; init; }

    public decimal? SquatKg { get; init; }

    public decimal? BenchKg { get; init; }

    public decimal? DeadliftKg { get; init; }

    public decimal? TotalKg { get; init; }

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
}
