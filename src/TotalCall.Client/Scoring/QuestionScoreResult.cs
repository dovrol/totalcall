namespace TotalCall.Client.Scoring;

public sealed record QuestionScoreResult(
    string GroupId,
    string QuestionId,
    decimal Points,
    decimal MaxPoints,
    string? Explanation = null);
