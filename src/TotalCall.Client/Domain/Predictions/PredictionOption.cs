namespace TotalCall.Client.Domain.Predictions;

public sealed record PredictionOption
{
    public required string Id { get; init; }

    public required string Label { get; init; }

    public string? Description { get; init; }
}
