using System.Text.Json;
using System.Text.Json.Nodes;
using TotalCall.Client.Domain.Competitions;
using TotalCall.Client.Domain.Predictions;
using TotalCall.Client.Scoring;

namespace TotalCall.Sync.Results;

public sealed class ScoreSnapshotBuilder(IPredictionScoringService scoringService)
{
    public const string RulesVersion = "placement-v1";

    public IReadOnlyList<ScoreSnapshotImportRow> Build(
        IReadOnlyList<PredictionSubmissionImportRow> submissions,
        IReadOnlyDictionary<string, Competition> competitionByVersionId,
        OfficialCompetitionResults officialResults,
        DateTimeOffset calculatedAt)
    {
        var rows = new List<ScoreSnapshotImportRow>();

        foreach (var submission in submissions)
        {
            if (!submission.IsSubmitted)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(submission.CompetitionVersionId))
            {
                continue;
            }

            if (!competitionByVersionId.TryGetValue(submission.CompetitionVersionId, out var competition))
            {
                continue;
            }

            var score = scoringService.Score(competition, submission.PredictionSet, officialResults);
            rows.Add(new ScoreSnapshotImportRow(
                submission.Id,
                submission.UserId,
                submission.CompetitionId,
                submission.CompetitionVersionId,
                score.TotalPoints,
                score.ScoredGroupsCount,
                score.TotalGroupsCount,
                score.Status,
                officialResults.ResultsHash ?? "unknown",
                RulesVersion,
                BuildBreakdown(score),
                calculatedAt));
        }

        return rows;
    }

    private static JsonObject BuildBreakdown(TotalScoreResult score)
    {
        var questionScores = new JsonArray();
        foreach (var questionScore in score.QuestionScores)
        {
            questionScores.Add(new JsonObject
            {
                ["groupId"] = questionScore.GroupId,
                ["questionId"] = questionScore.QuestionId,
                ["categoryId"] = questionScore.CategoryId,
                ["points"] = questionScore.Points,
                ["maxPoints"] = questionScore.MaxPoints,
                ["explanation"] = questionScore.Explanation
            });
        }

        return new JsonObject
        {
            ["questionScores"] = questionScores
        };
    }
}

public sealed record PredictionSubmissionImportRow(
    string Id,
    string UserId,
    string CompetitionId,
    string? CompetitionVersionId,
    string? Status,
    DateTimeOffset? SubmittedAt,
    PredictionSet PredictionSet)
{
    public bool IsSubmitted =>
        SubmittedAt is not null &&
        string.Equals(Status, PredictionSet.SubmittedSubmissionStatus, StringComparison.OrdinalIgnoreCase);
}

public sealed record ScoreSnapshotImportRow(
    string PredictionSubmissionId,
    string UserId,
    string CompetitionId,
    string CompetitionVersionId,
    decimal TotalPoints,
    int ScoredGroupsCount,
    int TotalGroupsCount,
    ScoreCalculationStatus Status,
    string ResultsHash,
    string RulesVersion,
    JsonObject BreakdownJson,
    DateTimeOffset CalculatedAt)
{
    public JsonObject ToJsonObject()
    {
        return new JsonObject
        {
            ["prediction_submission_id"] = PredictionSubmissionId,
            ["user_id"] = UserId,
            ["competition_id"] = CompetitionId,
            ["competition_version_id"] = CompetitionVersionId,
            ["total_points"] = TotalPoints,
            ["scored_groups_count"] = ScoredGroupsCount,
            ["total_groups_count"] = TotalGroupsCount,
            ["status"] = Status.ToString().ToLowerInvariant(),
            ["results_hash"] = ResultsHash,
            ["rules_version"] = RulesVersion,
            ["breakdown_json"] = JsonNode.Parse(BreakdownJson.ToJsonString())!,
            ["calculated_at"] = CalculatedAt.ToString("o")
        };
    }
}
