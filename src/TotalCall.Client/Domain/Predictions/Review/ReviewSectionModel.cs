namespace TotalCall.Client.Domain.Predictions.Review;

public sealed record ReviewSectionModel
{
    public required string Id { get; init; }

    public required string QuestionId { get; init; }

    public required string Title { get; init; }

    public string? GroupLabel { get; init; }

    public required PredictionCompletionStatus Status { get; init; }

    public int SelectedCount { get; init; }

    public int RequiredCount { get; init; }

    public ReviewSectionLayout Layout { get; init; } = ReviewSectionLayout.SimpleSummary;

    public IReadOnlyList<ReviewPickRowModel> Picks { get; init; } = [];

    public string? SummaryText { get; init; }

    public string EditHref { get; init; } = string.Empty;

    public int RemainingItems => Math.Max(0, RequiredCount - SelectedCount);

    public bool IsComplete => Status == PredictionCompletionStatus.Complete;

    public bool IsNotStarted => Status == PredictionCompletionStatus.NotStarted;

    public bool IsInProgress => Status == PredictionCompletionStatus.InProgress;
}
