using System.Text.Json.Serialization;

namespace TotalCall.Core.Domain.Competitions;

public sealed record CompetitionUpdate
{
    public string? Id { get; init; }

    public string Type { get; init; } = CompetitionUpdateTypes.General;

    [JsonPropertyName("occurred_at")]
    public DateTimeOffset? OccurredAt { get; init; }

    public string? Title { get; init; }

    public string? Body { get; init; }

    [JsonPropertyName("athlete_ids")]
    public IReadOnlyList<string> AthleteIds { get; init; } = [];

    public string? Source { get; init; }
}
