namespace TotalCall.Client.Domain.Predictions;

public sealed record PredictionValidationError(
    string GroupId,
    string QuestionId,
    string Message)
{
    public string MessageKey { get; init; } = Message;

    public IReadOnlyDictionary<string, string> Parameters { get; init; } =
        new Dictionary<string, string>();
}
