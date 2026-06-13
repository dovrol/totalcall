using System.Text.Json.Serialization;

namespace TotalCall.Sync;

// ---- Competition JSON DTOs (subset — fields the sync tool needs) ----
// Shared by the `athletes` and `competition` subcommands. The full competition
// config is also synced verbatim as JSONB by the competition subcommand; these
// typed DTOs only cover what the athlete roster import reads.

public sealed record CompetitionDefinition
{
    [JsonPropertyName("id")] public string Id { get; init; } = "";
    [JsonPropertyName("slug")] public string Slug { get; init; } = "";
    [JsonPropertyName("name")] public string Name { get; init; } = "";
    [JsonPropertyName("federation")] public string? Federation { get; init; }
    [JsonPropertyName("updates")] public List<CompetitionUpdateDefinition> Updates { get; init; } = [];
    [JsonPropertyName("athletes")] public List<CompetitionAthlete> Athletes { get; init; } = [];
}

public sealed record CompetitionUpdateDefinition
{
    [JsonPropertyName("id")] public string? Id { get; init; }
    [JsonPropertyName("type")] public string Type { get; init; } = "general";
    [JsonPropertyName("occurred_at")] public DateTimeOffset? OccurredAt { get; init; }
    [JsonPropertyName("title")] public string? Title { get; init; }
    [JsonPropertyName("body")] public string? Body { get; init; }
    [JsonPropertyName("athlete_ids")] public List<string> AthleteIds { get; init; } = [];
    [JsonPropertyName("source")] public string? Source { get; init; }
}

public sealed record CompetitionAthlete
{
    [JsonPropertyName("id")] public string Id { get; init; } = "";
    [JsonPropertyName("displayName")] public string DisplayName { get; init; } = "";
    [JsonPropertyName("status")] public string? Status { get; init; }
    [JsonPropertyName("sex")] public string? Sex { get; init; }
    [JsonPropertyName("countryCode")] public string? CountryCode { get; init; }
    [JsonPropertyName("countryName")] public string? CountryName { get; init; }
    [JsonPropertyName("withdrawn_at")] public DateTimeOffset? WithdrawnAt { get; init; }
    [JsonPropertyName("withdrawal_reason")] public string? WithdrawalReason { get; init; }
    [JsonPropertyName("withdrawal_source")] public string? WithdrawalSource { get; init; }
    [JsonPropertyName("updated_at")] public DateTimeOffset? UpdatedAt { get; init; }
    [JsonPropertyName("externalAthleteRefs")] public List<ExternalAthleteRefDto> ExternalAthleteRefs { get; init; } = [];
}

public sealed record ExternalAthleteRefDto
{
    [JsonPropertyName("source")] public string Source { get; init; } = "";
    [JsonPropertyName("name")] public string Name { get; init; } = "";
    [JsonPropertyName("externalId")] public string? ExternalId { get; init; }
}
