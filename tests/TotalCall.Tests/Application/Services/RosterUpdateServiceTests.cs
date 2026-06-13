using TotalCall.Client.Application.Services;
using TotalCall.Client.Domain.Athletes;
using TotalCall.Client.Domain.Competitions;
using TotalCall.Client.Domain.Predictions;

namespace TotalCall.Tests.Application.Services;

public sealed class RosterUpdateServiceTests
{
    [Fact]
    public void FindAffectedPicks_returns_scored_withdrawn_athletes()
    {
        var competition = CreateCompetition();
        var answer = new PredictionAnswer
        {
            GroupId = "group",
            QuestionId = "podium",
            QuestionType = PredictionQuestionType.AthleteRanking,
            Value = new PredictionAnswerValue
            {
                AthletePlacements =
                [
                    new AthletePlacementPick { Position = 1, AthleteId = "withdrawn", IsScored = true },
                    new AthletePlacementPick { Position = 2, AthleteId = "active", IsScored = true },
                    new AthletePlacementPick { Position = 4, AthleteId = "withdrawn", IsScored = false }
                ]
            }
        };

        var affected = RosterUpdateService.FindAffectedPicks(competition, [answer]);

        var pick = Assert.Single(affected);
        Assert.Equal("withdrawn", pick.AthleteId);
        Assert.Equal("Withdrawn Athlete", pick.AthleteName);
        Assert.Equal(1, pick.Position);
    }

    [Fact]
    public void FindAffectedPicks_does_not_treat_non_scored_field_rows_as_picks()
    {
        var competition = CreateCompetition();
        var answer = new PredictionAnswer
        {
            GroupId = "group",
            QuestionId = "podium",
            QuestionType = PredictionQuestionType.AthleteRanking,
            Value = new PredictionAnswerValue
            {
                AthletePlacements =
                [
                    new AthletePlacementPick { Position = 4, AthleteId = "withdrawn", IsScored = false }
                ]
            }
        };

        var affected = RosterUpdateService.FindAffectedPicks(competition, [answer]);

        Assert.Empty(affected);
    }

    [Fact]
    public void FindAffectedPicks_returns_multiple_withdrawn_athletes()
    {
        var competition = CreateCompetition();
        var answers = new[]
        {
            new PredictionAnswer
            {
                GroupId = "group",
                QuestionId = "podium",
                QuestionType = PredictionQuestionType.AthleteRanking,
                Value = new PredictionAnswerValue
                {
                    AthletePlacements =
                    [
                        new AthletePlacementPick { Position = 1, AthleteId = "withdrawn", IsScored = true },
                        new AthletePlacementPick { Position = 2, AthleteId = "withdrawn-2", IsScored = true },
                        new AthletePlacementPick { Position = 3, AthleteId = "active", IsScored = true }
                    ]
                }
            },
            new PredictionAnswer
            {
                GroupId = "group",
                QuestionId = "podium-2",
                QuestionType = PredictionQuestionType.AthleteRanking,
                Value = new PredictionAnswerValue
                {
                    AthletePlacements =
                    [
                        new AthletePlacementPick { Position = 1, AthleteId = "withdrawn-2", IsScored = true }
                    ]
                }
            }
        };

        var affected = RosterUpdateService.FindAffectedPicks(competition, answers);
        var affectedNames = RosterUpdateService.GetAffectedAthleteNames(competition, answers);

        Assert.Equal(3, affected.Count);
        Assert.Contains(affected, pick => pick.AthleteId == "withdrawn" && pick.Position == 1);
        Assert.Contains(affected, pick => pick.AthleteId == "withdrawn-2" && pick.QuestionId == "podium");
        Assert.Contains(affected, pick => pick.AthleteId == "withdrawn-2" && pick.QuestionId == "podium-2");
        Assert.Equal(["Second Withdrawn", "Withdrawn Athlete"], affectedNames);
    }

    [Fact]
    public void FindAffectedPicks_detects_single_and_multi_athlete_selection_values()
    {
        var competition = CreateCompetition();
        var answers = new[]
        {
            new PredictionAnswer
            {
                GroupId = "group",
                QuestionId = "single",
                QuestionType = PredictionQuestionType.SingleAthleteChoice,
                Value = new PredictionAnswerValue
                {
                    SelectedAthleteId = "withdrawn"
                }
            },
            new PredictionAnswer
            {
                GroupId = "group",
                QuestionId = "multi",
                QuestionType = PredictionQuestionType.MultiAthleteChoice,
                Value = new PredictionAnswerValue
                {
                    SelectedAthleteIds = ["active", "withdrawn-2"]
                }
            }
        };

        var affected = RosterUpdateService.FindAffectedPicks(competition, answers);

        Assert.Equal(2, affected.Count);
        Assert.Contains(affected, pick => pick.QuestionId == "single" && pick.AthleteId == "withdrawn" && pick.Position is null);
        Assert.Contains(affected, pick => pick.QuestionId == "multi" && pick.AthleteId == "withdrawn-2" && pick.Position is null);
    }

    [Fact]
    public void FindAffectedPicks_respects_group_and_question_filters()
    {
        var competition = CreateCompetition();
        var answers = new[]
        {
            new PredictionAnswer
            {
                GroupId = "women-47",
                QuestionId = "podium",
                QuestionType = PredictionQuestionType.AthleteRanking,
                Value = new PredictionAnswerValue
                {
                    AthletePlacements =
                    [
                        new AthletePlacementPick { Position = 1, AthleteId = "withdrawn", IsScored = true }
                    ]
                }
            },
            new PredictionAnswer
            {
                GroupId = "women-52",
                QuestionId = "podium",
                QuestionType = PredictionQuestionType.AthleteRanking,
                Value = new PredictionAnswerValue
                {
                    AthletePlacements =
                    [
                        new AthletePlacementPick { Position = 1, AthleteId = "withdrawn-2", IsScored = true }
                    ]
                }
            }
        };

        var affected = RosterUpdateService.FindAffectedPicks(
            competition,
            answers,
            groupId: "WOMEN-47",
            questionId: "PODIUM");

        var pick = Assert.Single(affected);
        Assert.Equal("women-47", pick.GroupId);
        Assert.Equal("podium", pick.QuestionId);
        Assert.Equal("withdrawn", pick.AthleteId);
    }

    [Fact]
    public void FindAffectedPicks_deduplicates_duplicate_withdrawn_selection_in_same_answer_and_position()
    {
        var competition = CreateCompetition();
        var answer = new PredictionAnswer
        {
            GroupId = "group",
            QuestionId = "podium",
            QuestionType = PredictionQuestionType.AthleteRanking,
            Value = new PredictionAnswerValue
            {
                AthletePlacements =
                [
                    new AthletePlacementPick { Position = 1, AthleteId = "withdrawn", IsScored = true },
                    new AthletePlacementPick { Position = 1, AthleteId = "WITHDRAWN", IsScored = true },
                    new AthletePlacementPick { Position = 2, AthleteId = "withdrawn", IsScored = true }
                ]
            }
        };

        var affected = RosterUpdateService.FindAffectedPicks(competition, [answer]);

        Assert.Equal(2, affected.Count);
        Assert.Contains(affected, pick => pick.AthleteId == "withdrawn" && pick.Position == 1);
        Assert.Contains(affected, pick => pick.AthleteId == "withdrawn" && pick.Position == 2);
    }

    [Fact]
    public void FindAffectedPicks_ignores_blank_unknown_and_active_athlete_ids()
    {
        var competition = CreateCompetition();
        var answer = new PredictionAnswer
        {
            GroupId = "group",
            QuestionId = "mixed",
            QuestionType = PredictionQuestionType.MultiAthleteChoice,
            Value = new PredictionAnswerValue
            {
                SelectedAthleteId = "active",
                SelectedAthleteIds = ["", "unknown", "ACTIVE"]
            }
        };

        var affected = RosterUpdateService.FindAffectedPicks(competition, [answer]);

        Assert.Empty(affected);
    }

    [Fact]
    public void GetWithdrawnAthletes_returns_only_withdrawn_athletes_sorted_by_name()
    {
        var competition = CreateCompetition();

        var withdrawn = RosterUpdateService.GetWithdrawnAthletes(competition);

        Assert.Equal(["Second Withdrawn", "Withdrawn Athlete"], withdrawn.Select(athlete => athlete.DisplayName));
    }

    private static Competition CreateCompetition()
    {
        return new Competition
        {
            Id = "competition",
            Slug = "competition",
            Name = "Competition",
            ConfigVersion = "1",
            Athletes =
            [
                new Athlete
                {
                    Id = "withdrawn",
                    DisplayName = "Withdrawn Athlete",
                    Status = AthleteStatus.Withdrawn
                },
                new Athlete
                {
                    Id = "withdrawn-2",
                    DisplayName = "Second Withdrawn",
                    Status = AthleteStatus.Withdrawn
                },
                new Athlete
                {
                    Id = "active",
                    DisplayName = "Active Athlete"
                }
            ]
        };
    }
}
