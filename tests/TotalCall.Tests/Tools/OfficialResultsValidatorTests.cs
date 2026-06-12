using TotalCall.Client.Domain.Athletes;
using TotalCall.Client.Domain.Competitions;
using TotalCall.Client.Domain.Predictions;
using TotalCall.Sync.Results;

namespace TotalCall.Tests.Tools;

public sealed class OfficialResultsValidatorTests
{
    private readonly OfficialResultsValidator validator = new();

    [Fact]
    public void Validate_rejects_group_id_missing_from_config()
    {
        var results = CreateResultsFile(new OfficialResultGroupFile
        {
            GroupId = "missing-group",
            QuestionId = "q1",
            Status = OfficialResultGroupImportStatus.Final,
            Placements = [Placement(1, "a1")]
        });

        var errors = validator.Validate(CreateCompetition(), results, "competition");

        Assert.Contains(errors, error => error.Contains("Unknown prediction group_id 'missing-group'"));
    }

    [Fact]
    public void Validate_rejects_athlete_id_not_allowed_by_question()
    {
        var results = CreateResultsFile(new OfficialResultGroupFile
        {
            GroupId = "group",
            QuestionId = "q1",
            CategoryId = "cat",
            Status = OfficialResultGroupImportStatus.Final,
            Placements = [Placement(1, "other")]
        });

        var errors = validator.Validate(CreateCompetition(), results, "competition");

        Assert.Contains(errors, error => error.Contains("is not allowed by the question config"));
    }

    [Fact]
    public void Validate_accepts_known_final_group()
    {
        var results = CreateResultsFile(new OfficialResultGroupFile
        {
            GroupId = "group",
            QuestionId = "q1",
            CategoryId = "cat",
            Status = OfficialResultGroupImportStatus.Final,
            Placements =
            [
                Placement(1, "a1"),
                Placement(2, "a2"),
                Placement(3, "a3")
            ]
        });

        var errors = validator.Validate(CreateCompetition(), results, "competition");

        Assert.Empty(errors);
    }

    private static OfficialResultsFile CreateResultsFile(params OfficialResultGroupFile[] groups)
    {
        return new OfficialResultsFile
        {
            CompetitionId = "competition",
            Status = OfficialResultImportStatus.Partial,
            Groups = groups.ToList()
        };
    }

    private static OfficialResultPlacementFile Placement(int position, string athleteId)
    {
        return new OfficialResultPlacementFile
        {
            Position = position,
            AthleteId = athleteId
        };
    }

    private static Competition CreateCompetition()
    {
        return new Competition
        {
            Id = "competition",
            Slug = "competition",
            Name = "Competition",
            ConfigVersion = "v1",
            Athletes =
            [
                Athlete("a1"),
                Athlete("a2"),
                Athlete("a3"),
                Athlete("other")
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
            PredictionGroups =
            [
                new PredictionGroup
                {
                    Id = "group",
                    Title = "Group",
                    Questions =
                    [
                        new PredictionQuestion
                        {
                            Id = "q1",
                            Type = PredictionQuestionType.AthleteRanking,
                            Title = "Question",
                            CategoryId = "cat",
                            AthleteIds = ["a1", "a2", "a3"],
                            Constraints = new PredictionQuestionConstraints
                            {
                                ExactSelections = 3
                            }
                        }
                    ]
                }
            ]
        };
    }

    private static Athlete Athlete(string id)
    {
        return new Athlete
        {
            Id = id,
            DisplayName = id
        };
    }
}
