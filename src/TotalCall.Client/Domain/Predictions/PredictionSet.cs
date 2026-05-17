namespace TotalCall.Client.Domain.Predictions;

public sealed record PredictionSet
{
    public required string CompetitionId { get; init; }

    public required string CompetitionConfigVersion { get; init; }

    public string? LocalUserId { get; init; }

    public DateTimeOffset SavedAt { get; init; } = DateTimeOffset.UtcNow;

    public IReadOnlyList<PredictionAnswer> Answers { get; init; } = [];
}
