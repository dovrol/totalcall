using System.Text.Json.Serialization;

namespace TotalCall.Operations.Results;

public sealed record OfficialResultsFile
{
    [JsonPropertyName("competitionId")]
    public string CompetitionId { get; init; } = "";

    [JsonPropertyName("status")]
    public string Status { get; init; } = OfficialResultImportStatus.Partial;

    [JsonPropertyName("source")]
    public string? Source { get; init; }

    [JsonPropertyName("groups")]
    public List<OfficialResultGroupFile> Groups { get; init; } = [];
}

public sealed record OfficialResultGroupFile
{
    [JsonPropertyName("groupId")]
    public string GroupId { get; init; } = "";

    [JsonPropertyName("questionId")]
    public string QuestionId { get; init; } = "";

    [JsonPropertyName("categoryId")]
    public string? CategoryId { get; init; }

    [JsonPropertyName("status")]
    public string Status { get; init; } = OfficialResultGroupImportStatus.Pending;

    [JsonPropertyName("placements")]
    public List<OfficialResultPlacementFile> Placements { get; init; } = [];
}

public sealed record OfficialResultPlacementFile
{
    [JsonPropertyName("position")]
    public int Position { get; init; }

    [JsonPropertyName("athleteId")]
    public string AthleteId { get; init; } = "";

    [JsonPropertyName("squatKg")]
    public decimal? SquatKg { get; init; }

    [JsonPropertyName("benchKg")]
    public decimal? BenchKg { get; init; }

    [JsonPropertyName("deadliftKg")]
    public decimal? DeadliftKg { get; init; }

    [JsonPropertyName("totalKg")]
    public decimal? TotalKg { get; init; }
}

public static class OfficialResultImportStatus
{
    public const string Partial = "partial";
    public const string Final = "final";
}

public static class OfficialResultGroupImportStatus
{
    public const string Pending = "pending";
    public const string Final = "final";
}
