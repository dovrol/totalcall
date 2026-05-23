namespace TotalCall.Client.Domain.Releases;

public sealed record ChangelogItem
{
    public string Type { get; init; } = "feat";

    public LocalizedText Text { get; init; } = new();

    public ChangelogItemType ResolvedType => Type?.ToLowerInvariant() switch
    {
        "feat" or "feature" => ChangelogItemType.Feature,
        "fix" or "bugfix" => ChangelogItemType.Fix,
        "improvement" or "refactor" or "perf" => ChangelogItemType.Improvement,
        _ => ChangelogItemType.Other
    };
}
