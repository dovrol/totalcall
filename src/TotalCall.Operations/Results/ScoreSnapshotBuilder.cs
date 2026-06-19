using System.Text.Json;
using System.Text.Json.Nodes;
using TotalCall.Core.Domain.Competitions;
using TotalCall.Core.Domain.Predictions;
using TotalCall.Core.Scoring;

namespace TotalCall.Operations.Results;

public sealed class ScoreSnapshotBuilder(IPredictionScoringService scoringService)
{
    public const string RulesVersion = "placement-v2";

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
                ["placement"] = questionScore.PlacementPoints,
                ["placementMax"] = questionScore.PlacementMax,
                ["setBonus"] = questionScore.SetBonus,
                ["orderBonus"] = questionScore.OrderBonus,
                ["explanation"] = questionScore.Explanation,
                ["slots"] = BuildSlots(questionScore.Slots),
                ["official"] = BuildOfficial(questionScore.Official)
            });
        }

        return new JsonObject
        {
            ["questionScores"] = questionScores
        };
    }

    private static JsonArray BuildSlots(IReadOnlyList<SlotScoreResult>? slots)
    {
        var array = new JsonArray();
        if (slots is null)
        {
            return array;
        }

        foreach (var slot in slots)
        {
            array.Add(new JsonObject
            {
                ["position"] = slot.Position,
                ["athleteId"] = slot.AthleteId,
                ["verdict"] = slot.Verdict,
                ["points"] = slot.Points
            });
        }

        return array;
    }

    private static JsonArray BuildOfficial(IReadOnlyList<OfficialPlacementRef>? official)
    {
        var array = new JsonArray();
        if (official is null)
        {
            return array;
        }

        foreach (var placement in official)
        {
            array.Add(new JsonObject
            {
                ["position"] = placement.Position,
                ["athleteId"] = placement.AthleteId,
                ["squatKg"] = placement.SquatKg,
                ["benchKg"] = placement.BenchKg,
                ["deadliftKg"] = placement.DeadliftKg,
                ["totalKg"] = placement.TotalKg
            });
        }

        return array;
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
