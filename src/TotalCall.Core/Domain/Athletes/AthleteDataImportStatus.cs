namespace TotalCall.Core.Domain.Athletes;

public sealed class AthleteDataImportStatus
{
    public string? Source { get; init; }
    public string? SourceLabel { get; init; }
    public DateTimeOffset? LastSuccessfulImportAt { get; init; }
}
