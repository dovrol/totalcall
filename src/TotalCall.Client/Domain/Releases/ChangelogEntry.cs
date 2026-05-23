namespace TotalCall.Client.Domain.Releases;

public sealed record ChangelogEntry
{
    public string Version { get; init; } = string.Empty;

    public DateOnly? ReleasedAt { get; init; }

    public LocalizedText? Title { get; init; }

    public IReadOnlyList<ChangelogItem> Items { get; init; } = [];
}
