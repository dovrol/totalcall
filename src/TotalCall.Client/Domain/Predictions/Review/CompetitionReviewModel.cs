namespace TotalCall.Client.Domain.Predictions.Review;

public sealed record CompetitionReviewModel
{
    public required string CompetitionId { get; init; }

    public required string CompetitionSlug { get; init; }

    public required string CompetitionName { get; init; }

    public DateTimeOffset? PredictionLockAt { get; init; }

    public DateTimeOffset SavedAt { get; init; }

    public bool CanEditPredictions { get; init; }

    public required ReviewCompletionStats Stats { get; init; }

    public IReadOnlyList<PredictionModuleReviewModel> Modules { get; init; } = [];

    public bool IsComplete =>
        Stats.TotalSections > 0 &&
        Stats.CompletedSections == Stats.TotalSections &&
        Stats.CompletedModules == Stats.TotalModules;

    public bool HasAnyAnswer => Stats.SavedPicks > 0;
}
