using TotalCall.Client.Application.Services;
using TotalCall.Client.Domain.Competitions;
using TotalCall.Client.Domain.Predictions;

namespace TotalCall.Tests.Application.Services;

public sealed class PredictionValidationServiceTests
{
    private readonly PredictionValidationService validator = new();

    [Fact]
    public void Validate_returns_error_for_missing_required_answer()
    {
        var competition = CreateCompetition(new PredictionQuestion
        {
            Id = "any-wr",
            Type = PredictionQuestionType.YesNo,
            Title = "Will any world record be broken?",
            Required = true
        });

        var result = validator.Validate(competition, CreatePredictionSet());

        var error = Assert.Single(result.Errors);
        Assert.Equal("any-wr", error.QuestionId);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_rejects_duplicate_athletes_on_podium()
    {
        var competition = CreateCompetition(new PredictionQuestion
        {
            Id = "podium",
            Type = PredictionQuestionType.CategoryPodium,
            Title = "Podium",
            Required = true,
            Constraints = new PredictionQuestionConstraints
            {
                ExactSelections = 3,
                DisallowDuplicateAthletes = true
            }
        });

        var predictionSet = CreatePredictionSet(new PredictionAnswer
        {
            GroupId = "group",
            QuestionId = "podium",
            QuestionType = PredictionQuestionType.CategoryPodium,
            Value = new PredictionAnswerValue
            {
                AthletePlacements =
                [
                    new AthletePlacementPick { Position = 1, AthleteId = "athlete-1" },
                    new AthletePlacementPick { Position = 2, AthleteId = "athlete-1" },
                    new AthletePlacementPick { Position = 3, AthleteId = "athlete-2" }
                ]
            }
        });

        var result = validator.Validate(competition, predictionSet);

        Assert.Contains(result.Errors, error => error.QuestionId == "podium" && error.Message.Contains("same athlete"));
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_counts_only_scored_athletes_from_full_field()
    {
        var competition = CreateCompetition(new PredictionQuestion
        {
            Id = "podium",
            Type = PredictionQuestionType.CategoryPodium,
            Title = "Podium",
            Required = true,
            Constraints = new PredictionQuestionConstraints
            {
                ExactSelections = 3,
                DisallowDuplicateAthletes = true
            }
        });

        var predictionSet = CreatePredictionSet(new PredictionAnswer
        {
            GroupId = "group",
            QuestionId = "podium",
            QuestionType = PredictionQuestionType.CategoryPodium,
            Value = new PredictionAnswerValue
            {
                AthletePlacements =
                [
                    new AthletePlacementPick { Position = 1, AthleteId = "athlete-1" },
                    new AthletePlacementPick { Position = 2, AthleteId = "athlete-2" },
                    new AthletePlacementPick { Position = 3, AthleteId = "athlete-3" },
                    new AthletePlacementPick { Position = 4, AthleteId = "athlete-4", IsScored = false }
                ]
            }
        });

        var result = validator.ValidateModule(competition, competition.PredictionGroups.Single(), predictionSet);

        Assert.Empty(result.ValidationErrors);
        Assert.Equal(PredictionCompletionStatus.Complete, result.Status);
        Assert.Equal(3, Assert.Single(result.Questions).SelectedCount);
    }

    [Fact]
    public void Validate_rejects_numeric_value_outside_configured_range()
    {
        var competition = CreateCompetition(new PredictionQuestion
        {
            Id = "highest-total",
            Type = PredictionQuestionType.NumericQuestion,
            Title = "Highest total",
            Required = true,
            Constraints = new PredictionQuestionConstraints
            {
                MinValue = 450,
                MaxValue = 1000
            }
        });

        var predictionSet = CreatePredictionSet(new PredictionAnswer
        {
            GroupId = "group",
            QuestionId = "highest-total",
            QuestionType = PredictionQuestionType.NumericQuestion,
            Value = new PredictionAnswerValue { NumericValue = 1200 }
        });

        var result = validator.Validate(competition, predictionSet);

        Assert.Contains(result.Errors, error => error.QuestionId == "highest-total" && error.Message.Contains("no more than"));
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_accepts_valid_yes_no_answer()
    {
        var competition = CreateCompetition(new PredictionQuestion
        {
            Id = "any-wr",
            Type = PredictionQuestionType.YesNo,
            Title = "Will any world record be broken?",
            Required = true
        });

        var predictionSet = CreatePredictionSet(new PredictionAnswer
        {
            GroupId = "group",
            QuestionId = "any-wr",
            QuestionType = PredictionQuestionType.YesNo,
            Value = new PredictionAnswerValue { BooleanValue = false }
        });

        var result = validator.Validate(competition, predictionSet);

        Assert.Empty(result.Errors);
        Assert.True(result.IsValid);
    }

    private static Competition CreateCompetition(PredictionQuestion question)
    {
        return new Competition
        {
            Id = "competition",
            Slug = "competition",
            Name = "Competition",
            ConfigVersion = "1",
            PredictionGroups =
            [
                new PredictionGroup
                {
                    Id = "group",
                    Title = "Group",
                    Questions = [question]
                }
            ]
        };
    }

    private static PredictionSet CreatePredictionSet(params PredictionAnswer[] answers)
    {
        return new PredictionSet
        {
            CompetitionId = "competition",
            CompetitionConfigVersion = "1",
            Answers = answers
        };
    }
}
