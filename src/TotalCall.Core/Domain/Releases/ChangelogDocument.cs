namespace TotalCall.Core.Domain.Releases;

public sealed record ChangelogDocument
{
    public int SchemaVersion { get; init; } = 1;

    public IReadOnlyList<ChangelogEntry> Entries { get; init; } = [];
}
