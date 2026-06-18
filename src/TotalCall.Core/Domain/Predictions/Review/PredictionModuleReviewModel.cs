namespace TotalCall.Core.Domain.Predictions.Review;

public sealed record PredictionModuleReviewModel
{
    public required string GroupId { get; init; }

    public required string ModuleType { get; init; }

    public required string Title { get; init; }

    public string? Description { get; init; }

    public required PredictionCompletionStatus Status { get; init; }

    public int TotalSections { get; init; }

    public int CompletedSections { get; init; }

    public int SavedPicks { get; init; }

    public int Order { get; init; }

    public string EditHref { get; init; } = string.Empty;

    public bool HasGroupedSections { get; init; }

    public IReadOnlyList<ReviewSectionModel> Sections { get; init; } = [];

    public int RemainingSections => Math.Max(0, TotalSections - CompletedSections);

    public bool IsComplete => Status == PredictionCompletionStatus.Complete;
}
