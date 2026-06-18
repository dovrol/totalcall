namespace TotalCall.Core.Domain.Predictions;

public sealed record PredictionModuleValidationResult(
    string GroupId,
    PredictionCompletionStatus Status,
    int TotalItems,
    int CompletedItems,
    int InProgressItems,
    int NotStartedItems,
    IReadOnlyList<PredictionQuestionCompletionResult> Questions,
    IReadOnlyList<PredictionValidationError> ValidationErrors)
{
    public int RemainingItems => Math.Max(0, TotalItems - CompletedItems);
}
