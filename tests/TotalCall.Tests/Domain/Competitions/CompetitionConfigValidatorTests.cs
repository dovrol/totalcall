using System.Text.Json;
using TotalCall.Core.Domain.Athletes;
using TotalCall.Core.Domain.Competitions;
using TotalCall.Core.Domain.Predictions;
using TotalCall.Core.Validation;

namespace TotalCall.Tests.Domain.Competitions;

public sealed class CompetitionConfigValidatorTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly CompetitionConfigValidator validator = new();

    [Fact]
    public void Validate_accepts_current_worlds_config()
    {
        var path = Path.Combine(
            FindRepositoryRoot(),
            "ops",
            "data",
            "competitions",
            "worlds-2026.json");
        var competition = JsonSerializer.Deserialize<Competition>(
            File.ReadAllText(path),
            JsonOptions);

        Assert.NotNull(competition);

        var result = validator.Validate(competition);

        Assert.True(
            result.IsValid,
            string.Join(Environment.NewLine, result.Errors.Select(error => $"{error.Path}: {error.Message}")));
    }

    [Fact]
    public void Validate_rejects_known_but_unsupported_module_type()
    {
        var competition = CreateValidCompetition() with
        {
            PredictionGroups =
            [
                CreateValidTopNGroup() with
                {
                    Type = PredictionModuleType.YesNo
                }
            ]
        };

        var result = validator.Validate(competition);

        Assert.Contains(result.Errors, error => error.Code == "UnsupportedModuleType");
    }

    [Fact]
    public void Validate_rejects_missing_prediction_lock()
    {
        var competition = CreateValidCompetition() with
        {
            PredictionLockAt = null
        };

        var result = validator.Validate(competition);

        Assert.Contains(result.Errors, error => error.Code == "PredictionLockAtRequired");
    }

    [Fact]
    public void Validate_rejects_topn_question_without_category()
    {
        var competition = CreateValidCompetitionWithQuestion(CreateValidQuestion() with
        {
            CategoryId = null
        });

        var result = validator.Validate(competition);

        Assert.Contains(result.Errors, error => error.Code == "TopNQuestionCategoryRequired");
    }

    [Fact]
    public void Validate_rejects_topn_question_with_unknown_athlete()
    {
        var competition = CreateValidCompetitionWithQuestion(CreateValidQuestion() with
        {
            AthleteIds = ["a1", "a2", "missing"]
        });

        var result = validator.Validate(competition);

        Assert.Contains(result.Errors, error => error.Code == "UnknownQuestionAthlete");
    }

    [Fact]
    public void Validate_rejects_topn_question_without_explicit_top3_constraint()
    {
        var competition = CreateValidCompetitionWithQuestion(CreateValidQuestion() with
        {
            Constraints = new PredictionQuestionConstraints
            {
                ExactSelections = 5,
                DisallowDuplicateAthletes = true
            }
        });

        var result = validator.Validate(competition);

        Assert.Contains(result.Errors, error => error.Code == "TopNExactSelectionsRequired");
    }

    [Fact]
    public void Validate_rejects_duplicate_athlete_ids()
    {
        var competition = CreateValidCompetition() with
        {
            Athletes =
            [
                Athlete("a1"),
                Athlete("a1"),
                Athlete("a3")
            ]
        };

        var result = validator.Validate(competition);

        Assert.Contains(result.Errors, error => error.Code == "DuplicateAthleteId");
    }

    private static Competition CreateValidCompetitionWithQuestion(PredictionQuestion question)
    {
        return CreateValidCompetition() with
        {
            PredictionGroups =
            [
                CreateValidTopNGroup() with
                {
                    Questions = [question]
                }
            ]
        };
    }

    private static Competition CreateValidCompetition()
    {
        return new Competition
        {
            Id = "competition",
            Slug = "competition",
            Name = "Competition",
            ConfigVersion = "v1",
            StartDate = DateTimeOffset.Parse("2026-06-13T07:00:00Z"),
            EndDate = DateTimeOffset.Parse("2026-06-21T14:00:00Z"),
            PredictionOpenAt = DateTimeOffset.Parse("2026-05-01T09:00:00Z"),
            PredictionLockAt = DateTimeOffset.Parse("2026-06-13T06:00:00Z"),
            Athletes =
            [
                Athlete("a1"),
                Athlete("a2"),
                Athlete("a3")
            ],
            Categories =
            [
                new WeightCategory
                {
                    Id = "cat",
                    Name = "Category",
                    AthleteIds = ["a1", "a2", "a3"]
                }
            ],
            PredictionGroups = [CreateValidTopNGroup()]
        };
    }

    private static PredictionGroup CreateValidTopNGroup()
    {
        return new PredictionGroup
        {
            Id = "top-n",
            Type = PredictionModuleType.TopNByCategory,
            Title = "Top N",
            Mode = "full",
            Questions = [CreateValidQuestion()]
        };
    }

    private static PredictionQuestion CreateValidQuestion()
    {
        return new PredictionQuestion
        {
            Id = "q1",
            Type = PredictionQuestionType.AthleteRanking,
            Title = "Top 3",
            CategoryId = "cat",
            AthleteIds = ["a1", "a2", "a3"],
            Constraints = new PredictionQuestionConstraints
            {
                ExactSelections = 3,
                DisallowDuplicateAthletes = true
            }
        };
    }

    private static Athlete Athlete(string id)
    {
        return new Athlete
        {
            Id = id,
            DisplayName = id,
            WeightCategoryId = "cat"
        };
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "TotalCall.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("Could not find repository root.");
    }
}
