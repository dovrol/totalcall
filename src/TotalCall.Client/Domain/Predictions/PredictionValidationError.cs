namespace TotalCall.Client.Domain.Predictions;

public sealed record PredictionValidationError(
    string GroupId,
    string QuestionId,
    string Message);
