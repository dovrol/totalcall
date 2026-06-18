using TotalCall.Core.Domain.Competitions;
using TotalCall.Core.Domain.Athletes;
using TotalCall.Core.Domain.Predictions;
using TotalCall.Core.Scoring;

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

        // q1 is a perfect Top 3: 3+3+3 placement + 1 set + 2 order = 12.
        Assert.Equal(12m, score.TotalPoints);
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

        // Two perfect Top 3 groups: 12 + 12.
        Assert.Equal(24m, score.TotalPoints);
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

        // a1 exact (+3), a3 correct-wrong-slot (+1), a4 miss (+0); no bonus (a2 missing).
        Assert.Equal(4m, score.TotalPoints);

        var question = Assert.Single(score.QuestionScores);
        Assert.Equal(4m, question.PlacementPoints);
        Assert.Equal(9m, question.PlacementMax);
        Assert.Equal(0m, question.SetBonus);
        Assert.Equal(0m, question.OrderBonus);
        Assert.Equal(12m, question.MaxPoints);
        Assert.NotNull(question.Slots);
        Assert.Collection(question.Slots!,
            slot => Assert.Equal(SlotVerdict.Exact, slot.Verdict),
            slot => Assert.Equal(SlotVerdict.Wrong, slot.Verdict),
            slot => Assert.Equal(SlotVerdict.Miss, slot.Verdict));
        Assert.NotNull(question.Official);
        Assert.Equal(3, question.Official!.Count);
    }

    [Fact]
    public void Placement_scorer_awards_set_bonus_without_perfect_order()
    {
        var competition = CreateCompetition("q1");
        var predictionSet = CreatePredictionSet(Answer("q1", "a1", "a3", "a2"));
        var results = CreateResults(Result("q1", OfficialResultGroupStatus.Final, "a1", "a2", "a3"));

        var score = scoring.Score(competition, predictionSet, results);

        // a1 exact (+3), a3/a2 swapped (+1 each) = 5 placement, all three present (+1 set), not perfect.
        var question = Assert.Single(score.QuestionScores);
        Assert.Equal(5m, question.PlacementPoints);
        Assert.Equal(1m, question.SetBonus);
        Assert.Equal(0m, question.OrderBonus);
        Assert.Equal(6m, score.TotalPoints);
    }

    [Fact]
    public void Placement_scorer_awards_perfect_order_bonus()
    {
        var competition = CreateCompetition("q1");
        var predictionSet = CreatePredictionSet(Answer("q1", "a1", "a2", "a3"));
        var results = CreateResults(Result("q1", OfficialResultGroupStatus.Final, "a1", "a2", "a3"));

        var score = scoring.Score(competition, predictionSet, results);

        var question = Assert.Single(score.QuestionScores);
        Assert.Equal(9m, question.PlacementPoints);
        Assert.Equal(1m, question.SetBonus);
        Assert.Equal(2m, question.OrderBonus);
        Assert.Equal(12m, score.TotalPoints);
        Assert.All(question.Slots!, slot => Assert.Equal(SlotVerdict.Exact, slot.Verdict));
    }

    [Fact]
    public void Placement_scorer_marks_withdrawn_pick_and_skips_set_bonus()
    {
        var competition = CreateCompetition("q1");
        var predictionSet = CreatePredictionSet(Answer("q1", "withdrawn", "a2", "a3"));
        var results = CreateResults(Result("q1", OfficialResultGroupStatus.Final, "a4", "a2", "a3"));

        var score = scoring.Score(competition, predictionSet, results);

        var question = Assert.Single(score.QuestionScores);
        Assert.Equal(6m, score.TotalPoints);
        Assert.Equal(0m, question.SetBonus);
        Assert.Equal(SlotVerdict.Withdrawn, question.Slots![0].Verdict);
        Assert.Equal(0m, question.Slots![0].Points);
    }

    [Fact]
    public void Placement_scorer_counts_remaining_hits_when_withdrawn_pick_misses()
    {
        var competition = CreateCompetition("q1");
        var predictionSet = CreatePredictionSet(Answer("q1", "withdrawn", "a2", "a3"));
        var results = CreateResults(Result("q1", OfficialResultGroupStatus.Final, "a4", "a2", "a3"));

        var score = scoring.Score(competition, predictionSet, results);

        Assert.Equal(6m, score.TotalPoints);
        Assert.Equal(6m, Assert.Single(score.QuestionScores).Points);
    }

    private static Competition CreateCompetition(params string[] questionIds)
    {
        return new Competition
        {
            Id = "competition",
            Slug = "competition",
            Name = "Competition",
            ConfigVersion = "v1",
            Athletes =
            [
                new Athlete { Id = "a1", DisplayName = "A1" },
                new Athlete { Id = "a2", DisplayName = "A2" },
                new Athlete { Id = "a3", DisplayName = "A3" },
                new Athlete { Id = "a4", DisplayName = "A4" },
                new Athlete { Id = "withdrawn", DisplayName = "Withdrawn", Status = AthleteStatus.Withdrawn }
            ],
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
