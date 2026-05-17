namespace TotalCall.Client.Domain.Predictions;

public sealed record PredictionValidationResult
{
    public PredictionValidationResult(IReadOnlyList<PredictionValidationError> errors)
    {
        Errors = errors;
    }

    public IReadOnlyList<PredictionValidationError> Errors { get; }

    public bool IsValid => Errors.Count == 0;

    public IReadOnlyList<PredictionValidationError> GetErrorsForQuestion(string questionId)
    {
        return Errors
            .Where(error => error.QuestionId == questionId)
            .ToArray();
    }
}
