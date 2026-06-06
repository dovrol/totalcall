using System.Text.Json.Serialization;

namespace TotalCall.Client.Domain.Predictions;

public sealed record PredictionSet
{
    public const int StorageSchemaVersion = 1;
    public const string DraftSubmissionStatus = "draft";
    public const string SubmittedSubmissionStatus = "submitted";

    public required string CompetitionId { get; init; }

    public required string CompetitionConfigVersion { get; init; }

    public string? LocalUserId { get; init; }

    public string AppVersion { get; init; } = string.Empty;

    public int SchemaVersion { get; init; } = StorageSchemaVersion;

    public DateTimeOffset SavedAt { get; init; } = DateTimeOffset.UtcNow;

    public string SubmissionStatus { get; init; } = DraftSubmissionStatus;

    public DateTimeOffset? SubmittedAt { get; init; }

    public IReadOnlyList<PredictionAnswer> Answers { get; init; } = [];

    [JsonIgnore]
    public bool IsSubmitted => string.Equals(
        SubmissionStatus,
        SubmittedSubmissionStatus,
        StringComparison.OrdinalIgnoreCase);
}
