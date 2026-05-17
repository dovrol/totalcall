namespace TotalCall.Client.Scoring;

public sealed record TotalScoreResult(
    decimal TotalPoints,
    IReadOnlyList<QuestionScoreResult> QuestionScores);
