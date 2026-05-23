namespace TotalCall.Client.Domain.Predictions;

public sealed record PredictionSet
{
    public const int StorageSchemaVersion = 1;

    public required string CompetitionId { get; init; }

    public required string CompetitionConfigVersion { get; init; }

    public string? LocalUserId { get; init; }

    public string AppVersion { get; init; } = string.Empty;

    public int SchemaVersion { get; init; } = StorageSchemaVersion;

    public DateTimeOffset SavedAt { get; init; } = DateTimeOffset.UtcNow;

    public IReadOnlyList<PredictionAnswer> Answers { get; init; } = [];
}
