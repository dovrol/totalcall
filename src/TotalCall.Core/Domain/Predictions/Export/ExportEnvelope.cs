namespace TotalCall.Core.Domain.Predictions.Export;

public sealed record ExportEnvelope
{
    public required string CompetitionId { get; init; }
    public required string CompetitionName { get; init; }
    public required string AppVersion { get; init; }
    public required int SchemaVersion { get; init; }
    public required DateTimeOffset ExportedAt { get; init; }
    public required DateTimeOffset SavedAt { get; init; }
}
