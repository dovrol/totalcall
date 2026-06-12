using TotalCall.Client.Domain.Competitions;
using TotalCall.Client.Domain.Predictions;
using TotalCall.Client.Scoring;

namespace TotalCall.Tests.Scoring;

public sealed class PredictionScoringServiceTests
{
    private readonly PredictionScoringService scoring = new(
    [
        new AthleteRankingQuestionScorer(),
        new CategoryPodiumQuestionScorer()
    ]);

    [Fact]
    public void Score_counts_only_final_groups_and_marks_leaderboard_partial()
    {
        var competition = CreateCompetition("q1", "q2");
        var predictionSet = CreatePredictionSet(
            Answer("q1", "a1", "a2", "a3"),
            Answer("q2", "b3", "b2", "b1"));
        var results = CreateResults(
            Result("q1", OfficialResultGroupStatus.Final, "a1", "a2", "a3"),
            Result("q2", OfficialResultGroupStatus.Pending, "b1", "b2", "b3"));

        var score = scoring.Score(competition, predictionSet, results);

        Assert.Equal(9m, score.TotalPoints);
        Assert.Equal(1, score.ScoredGroupsCount);
        Assert.Equal(2, score.TotalGroupsCount);
        Assert.Equal(ScoreCalculationStatus.Partial, score.Status);
        Assert.Single(score.QuestionScores);
    }

    [Fact]
    public void Score_marks_final_when_all_required_groups_are_final()
    {
        var competition = CreateCompetition("q1", "q2");
        var predictionSet = CreatePredictionSet(
            Answer("q1", "a1", "a2", "a3"),
            Answer("q2", "b1", "b2", "b3"));
        var results = CreateResults(
            Result("q1", OfficialResultGroupStatus.Final, "a1", "a2", "a3"),
            Result("q2", OfficialResultGroupStatus.Final, "b1", "b2", "b3"));

        var score = scoring.Score(competition, predictionSet, results);

        Assert.Equal(18m, score.TotalPoints);
        Assert.Equal(2, score.ScoredGroupsCount);
        Assert.Equal(2, score.TotalGroupsCount);
        Assert.Equal(ScoreCalculationStatus.Final, score.Status);
    }

    [Fact]
    public void Score_counts_final_group_but_gives_zero_for_incomplete_prediction()
    {
        var competition = CreateCompetition("q1");
        var predictionSet = CreatePredictionSet(Answer("q1", "a1", "a2"));
        var results = CreateResults(Result("q1", OfficialResultGroupStatus.Final, "a1", "a2", "a3"));

        var score = scoring.Score(competition, predictionSet, results);

        Assert.Equal(0m, score.TotalPoints);
        Assert.Equal(1, score.ScoredGroupsCount);
        Assert.Equal(1, score.TotalGroupsCount);
        Assert.Equal(ScoreCalculationStatus.Final, score.Status);
        Assert.Equal("Incomplete prediction.", Assert.Single(score.QuestionScores).Explanation);
    }

    [Fact]
    public void Placement_scorer_awards_exact_and_wrong_position_hits()
    {
        var competition = CreateCompetition("q1");
        var predictionSet = CreatePredictionSet(Answer("q1", "a1", "a3", "a4"));
        var results = CreateResults(Result("q1", OfficialResultGroupStatus.Final, "a1", "a2", "a3"));

        var score = scoring.Score(competition, predictionSet, results);

        Assert.Equal(4m, score.TotalPoints);
    }

    private static Competition CreateCompetition(params string[] questionIds)
    {
        return new Competition
        {
            Id = "competition",
            Slug = "competition",
            Name = "Competition",
            ConfigVersion = "v1",
            PredictionGroups =
            [
                new PredictionGroup
                {
                    Id = "group",
                    Title = "Group",
                    Required = true,
                    Questions = questionIds.Select((questionId, index) => new PredictionQuestion
                    {
                        Id = questionId,
                        Type = PredictionQuestionType.AthleteRanking,
                        Title = questionId,
                        Order = index + 1,
                        Required = true,
                        Constraints = new PredictionQuestionConstraints
                        {
                            ExactSelections = 3
                        }
                    }).ToArray()
                }
            ]
        };
    }

    private static PredictionSet CreatePredictionSet(params PredictionAnswer[] answers)
    {
        return new PredictionSet
        {
            CompetitionId = "competition",
            CompetitionConfigVersion = "v1",
            SubmissionStatus = PredictionSet.SubmittedSubmissionStatus,
            SubmittedAt = DateTimeOffset.Parse("2026-06-10T12:00:00Z"),
            Answers = answers
        };
    }

    private static PredictionAnswer Answer(string questionId, params string[] athleteIds)
    {
        return new PredictionAnswer
        {
            GroupId = "group",
            QuestionId = questionId,
            QuestionType = PredictionQuestionType.AthleteRanking,
            Value = new PredictionAnswerValue
            {
                AthletePlacements = athleteIds.Select((athleteId, index) => new AthletePlacementPick
                {
                    Position = index + 1,
                    AthleteId = athleteId,
                    IsScored = true
                }).ToArray()
            }
        };
    }

    private static OfficialCompetitionResults CreateResults(params OfficialResultGroup[] groups)
    {
        return new OfficialCompetitionResults
        {
            CompetitionId = "competition",
            ResultsHash = "hash",
            Groups = groups
        };
    }

    private static OfficialResultGroup Result(
        string questionId,
        OfficialResultGroupStatus status,
        params string[] athleteIds)
    {
        return new OfficialResultGroup
        {
            GroupId = "group",
            QuestionId = questionId,
            Status = status,
            Placements = athleteIds.Select((athleteId, index) => new OfficialAthleteResult
            {
                Position = index + 1,
                AthleteId = athleteId
            }).ToArray()
        };
    }
}
