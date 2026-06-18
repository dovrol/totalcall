namespace TotalCall.Core.Scoring;

public sealed record TotalScoreResult(
    decimal TotalPoints,
    IReadOnlyList<QuestionScoreResult> QuestionScores,
    int ScoredGroupsCount,
    int TotalGroupsCount,
    ScoreCalculationStatus Status);
