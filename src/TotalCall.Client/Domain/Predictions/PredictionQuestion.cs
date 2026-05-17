namespace TotalCall.Client.Domain.Predictions;

public sealed record PredictionQuestion
{
    public required string Id { get; init; }

    public required PredictionQuestionType Type { get; init; }

    public required string Title { get; init; }

    public string? Description { get; init; }

    public int Order { get; init; }

    public bool Required { get; init; } = true;

    public string? CategoryId { get; init; }

    public IReadOnlyList<string> AthleteIds { get; init; } = [];

    public IReadOnlyList<PredictionOption> Options { get; init; } = [];

    public PredictionQuestionConstraints Constraints { get; init; } = new();
}
