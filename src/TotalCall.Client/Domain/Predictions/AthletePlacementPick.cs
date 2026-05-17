namespace TotalCall.Client.Domain.Predictions;

public sealed record AthletePlacementPick
{
    public required int Position { get; init; }

    public required string AthleteId { get; init; }
}
