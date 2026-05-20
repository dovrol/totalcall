namespace TotalCall.Client.Domain.Predictions;

public sealed record PredictionQuestionCompletionResult(
    string QuestionId,
    PredictionCompletionStatus Status,
    int SelectedCount,
    int RequiredCount);
