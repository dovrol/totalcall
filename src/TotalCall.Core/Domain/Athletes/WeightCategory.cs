namespace TotalCall.Core.Domain.Athletes;

public sealed record WeightCategory
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public AthleteSex Sex { get; init; } = AthleteSex.Unspecified;

    public decimal? WeightLimitKg { get; init; }

    public IReadOnlyList<string> AthleteIds { get; init; } = [];
}
