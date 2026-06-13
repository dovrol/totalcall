using System.Text.Json.Serialization;

namespace TotalCall.Client.Domain.Athletes;

public sealed record Athlete
{
    public required string Id { get; init; }

    public required string DisplayName { get; init; }

    public AthleteStatus Status { get; init; } = AthleteStatus.Active;

    public AthleteSex Sex { get; init; } = AthleteSex.Unspecified;

    public string? CountryCode { get; init; }

    public string? CountryName { get; init; }

    public string? WeightCategoryId { get; init; }

    public decimal? BodyweightKg { get; init; }

    public decimal? SeedTotalKg { get; init; }

    public decimal? PersonalBestTotalKg { get; init; }

    public decimal? WorldRecordReferenceKg { get; init; }

    [JsonPropertyName("withdrawn_at")]
    public DateTimeOffset? WithdrawnAt { get; init; }

    [JsonPropertyName("withdrawal_reason")]
    public string? WithdrawalReason { get; init; }

    [JsonPropertyName("withdrawal_source")]
    public string? WithdrawalSource { get; init; }

    [JsonPropertyName("updated_at")]
    public DateTimeOffset? UpdatedAt { get; init; }

    public IReadOnlyList<ExternalAthleteRef> ExternalAthleteRefs { get; init; } = [];

    [JsonIgnore]
    public bool IsWithdrawn => Status == AthleteStatus.Withdrawn;
}
