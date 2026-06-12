using TotalCall.Client.Domain.Competitions;
using TotalCall.Client.Domain.Predictions;
using TotalCall.Client.Scoring;
using TotalCall.Sync.Results;

namespace TotalCall.Tests.Tools;

public sealed class ScoreSnapshotBuilderTests
{
    private readonly ScoreSnapshotBuilder builder = new(new PredictionScoringService(
    [
        new AthleteRankingQuestionScorer(),
        new CategoryPodiumQuestionScorer()
    ]));

    [Fact]
    public void Build_scores_submitted_predictions_only()
    {
        var submissions = new[]
        {
            Submission("submission-1", "user-1", "version-1", PredictionSet.SubmittedSubmissionStatus, submittedAt: true),
            Submission("submission-2", "user-2", "version-1", PredictionSet.DraftSubmissionStatus, submittedAt: false)
        };

        var rows = builder.Build(
            submissions,
            new Dictionary<string, Competition>(StringComparer.OrdinalIgnoreCase)
            {
                ["version-1"] = CompetitionWithQuestions("v1", "q1")
            },
            ResultsForQ1(),
            DateTimeOffset.Parse("2026-06-11T12:00:00Z"));

        var row = Assert.Single(rows);
        Assert.Equal("submission-1", row.PredictionSubmissionId);
        Assert.Equal("user-1", row.UserId);
    }

    [Fact]
    public void Build_uses_submission_competition_version_for_total_group_count()
    {
        var submissions = new[]
        {
            Submission("submission-1", "user-1", "version-1", PredictionSet.SubmittedSubmissionStatus, submittedAt: true),
            Submission("submission-2", "user-2", "version-2", PredictionSet.SubmittedSubmissionStatus, submittedAt: true)
        };

        var rows = builder.Build(
            submissions,
            new Dictionary<string, Competition>(StringComparer.OrdinalIgnoreCase)
            {
                ["version-1"] = CompetitionWithQuestions("v1", "q1"),
                ["version-2"] = CompetitionWithQuestions("v2", "q1", "q2")
            },
            ResultsForQ1(),
            DateTimeOffset.Parse("2026-06-11T12:00:00Z"));

        var v1 = Assert.Single(rows, row => row.UserId == "user-1");
        var v2 = Assert.Single(rows, row => row.UserId == "user-2");

        Assert.Equal(1, v1.TotalGroupsCount);
        Assert.Equal(1, v1.ScoredGroupsCount);
        Assert.Equal(ScoreCalculationStatus.Final, v1.Status);

        Assert.Equal(2, v2.TotalGroupsCount);
        Assert.Equal(1, v2.ScoredGroupsCount);
        Assert.Equal(ScoreCalculationStatus.Partial, v2.Status);
    }

    private static PredictionSubmissionImportRow Submission(
        string submissionId,
        string userId,
        string versionId,
        string status,
        bool submittedAt)
    {
        var predictionSet = new PredictionSet
        {
            CompetitionId = "competition",
            CompetitionConfigVersion = versionId,
            SubmissionStatus = status,
            SubmittedAt = submittedAt ? DateTimeOffset.Parse("2026-06-10T12:00:00Z") : null,
            Answers =
            [
                new PredictionAnswer
                {
                    GroupId = "group",
                    QuestionId = "q1",
                    QuestionType = PredictionQuestionType.AthleteRanking,
                    Value = new PredictionAnswerValue
                    {
                        AthletePlacements =
                        [
                            Pick(1, "a1"),
                            Pick(2, "a2"),
                            Pick(3, "a3")
                        ]
                    }
                }
            ]
        };

        return new PredictionSubmissionImportRow(
            submissionId,
            userId,
            "competition",
            versionId,
            status,
            submittedAt ? DateTimeOffset.Parse("2026-06-10T12:00:00Z") : null,
            predictionSet);
    }

    private static Competition CompetitionWithQuestions(string version, params string[] questionIds)
    {
        return new Competition
        {
            Id = "competition",
            Slug = "competition",
            Name = "Competition",
            ConfigVersion = version,
            PredictionGroups =
            [
                new PredictionGroup
                {
                    Id = "group",
                    Title = "Group",
                    Questions = questionIds.Select((questionId, index) => new PredictionQuestion
                    {
                        Id = questionId,
                        Type = PredictionQuestionType.AthleteRanking,
                        Title = questionId,
                        Order = index,
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

    private static OfficialCompetitionResults ResultsForQ1()
    {
        return new OfficialCompetitionResults
        {
            CompetitionId = "competition",
            ResultsHash = "results-hash",
            Groups =
            [
                new OfficialResultGroup
                {
                    GroupId = "group",
                    QuestionId = "q1",
                    Status = OfficialResultGroupStatus.Final,
                    Placements =
                    [
                        new OfficialAthleteResult { Position = 1, AthleteId = "a1" },
                        new OfficialAthleteResult { Position = 2, AthleteId = "a2" },
                        new OfficialAthleteResult { Position = 3, AthleteId = "a3" }
                    ]
                }
            ]
        };
    }

    private static AthletePlacementPick Pick(int position, string athleteId)
    {
        return new AthletePlacementPick
        {
            Position = position,
            AthleteId = athleteId,
            IsScored = true
        };
    }
}
