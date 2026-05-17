namespace TotalCall.Client.Domain.Predictions;

public sealed record PredictionGroup
{
    public required string Id { get; init; }

    public required string Title { get; init; }

    public string? Description { get; init; }

    public string? Mode { get; init; }

    public int Order { get; init; }

    public bool Required { get; init; } = true;

    public IReadOnlyList<PredictionQuestion> Questions { get; init; } = [];
}
