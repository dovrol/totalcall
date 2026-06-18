namespace TotalCall.Core.Domain.Predictions;

public sealed record PredictionAnswer
{
    public required string GroupId { get; init; }

    public required string QuestionId { get; init; }

    public required PredictionQuestionType QuestionType { get; init; }

    public PredictionAnswerValue Value { get; init; } = new();

    public DateTimeOffset UpdatedAt { get; init; } = DateTimeOffset.UtcNow;
}
