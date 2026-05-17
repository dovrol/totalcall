namespace TotalCall.Client.Domain.Athletes;

public sealed record Athlete
{
    public required string Id { get; init; }

    public required string DisplayName { get; init; }

    public AthleteSex Sex { get; init; } = AthleteSex.Unspecified;

    public string? CountryCode { get; init; }

    public string? CountryName { get; init; }

    public string? WeightCategoryId { get; init; }

    public decimal? BodyweightKg { get; init; }

    public decimal? SeedTotalKg { get; init; }

    public decimal? PersonalBestTotalKg { get; init; }

    public decimal? WorldRecordReferenceKg { get; init; }
}
