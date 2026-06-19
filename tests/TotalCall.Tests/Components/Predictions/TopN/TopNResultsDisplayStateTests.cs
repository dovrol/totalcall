using TotalCall.Client.Components.Predictions.TopN;
using TotalCall.Core.Domain.Predictions;
using TotalCall.Core.Domain.Predictions.Results;

namespace TotalCall.Tests.Components.Predictions.TopN;

public sealed class TopNResultsDisplayStateTests
{
    [Fact]
    public void Resolve_WhenScoreIsMissing_UsesCompetitionConfigFallbacks()
    {
        var group = CreateGroup();

        var state = TopNResultsDisplayState.Resolve(group, group.Questions[0], score: null);

        Assert.False(state.HasResultsImported);
        Assert.False(state.IsActiveCategoryScored);
        Assert.Null(state.ActiveBreakdown);
        Assert.Null(state.ActiveSheetResults);
        Assert.Equal(["women-52", "women-57"], state.PlacementQuestions.Select(question => question.Id));
        Assert.Equal(2, state.ResultsTotalCount);
        Assert.Equal(30m, state.MaxResultsPoints);
    }

    [Fact]
    public void Resolve_WhenScoreHasActiveBreakdown_MapsOfficialAndScoredSlots()
    {
        var group = CreateGroup();
        var score = new MyScoreSnapshot
        {
            TotalGroupsCount = 4,
            Categories =
            [
                new CategoryScoreBreakdown
                {
                    GroupId = "top-n",
                    QuestionId = "women-52",
                    Points = 3,
                    Slots =
                    [
                        new CategorySlotResult
                        {
                            Position = 1,
                            AthleteId = "athlete-a",
                            Verdict = ResultVerdict.Exact,
                            Points = 3
                        },
                        new CategorySlotResult
                        {
                            Position = 2,
                            AthleteId = "athlete-c",
                            Verdict = ResultVerdict.Miss,
                            Points = 0
                        }
                    ],
                    Official =
                    [
                        new CategoryOfficialPlacement
                        {
                            Position = 1,
                            AthleteId = "athlete-a",
                            SquatKg = 250,
                            BenchKg = 170,
                            DeadliftKg = 280,
                            TotalKg = 700
                        },
                        new CategoryOfficialPlacement
                        {
                            Position = 2,
                            AthleteId = "athlete-b",
                            TotalKg = 690
                        }
                    ]
                }
            ]
        };

        var state = TopNResultsDisplayState.Resolve(group, group.Questions[0], score);

        Assert.True(state.HasResultsImported);
        Assert.True(state.IsActiveCategoryScored);
        Assert.Equal(4, state.ResultsTotalCount);
        Assert.NotNull(state.ActiveSheetResults);

        var athleteA = state.ActiveSheetResults["athlete-a"];
        Assert.Equal(1, athleteA.OfficialPlace);
        Assert.Equal(ResultVerdict.Exact, athleteA.Verdict);
        Assert.Equal(3m, athleteA.Points);
        Assert.Equal(700m, athleteA.OfficialTotalKg);
        Assert.Equal(250m, athleteA.OfficialSquatKg);

        var athleteB = state.ActiveSheetResults["athlete-b"];
        Assert.Equal(2, athleteB.OfficialPlace);
        Assert.Null(athleteB.Verdict);
        Assert.Null(athleteB.Points);
        Assert.Equal(690m, athleteB.OfficialTotalKg);

        var athleteC = state.ActiveSheetResults["athlete-c"];
        Assert.Null(athleteC.OfficialPlace);
        Assert.Equal(ResultVerdict.Miss, athleteC.Verdict);
        Assert.Equal(0m, athleteC.Points);
    }

    [Fact]
    public void Resolve_WhenScoreDoesNotContainActiveQuestion_HasNoActiveSheetResults()
    {
        var group = CreateGroup();
        var score = new MyScoreSnapshot
        {
            TotalGroupsCount = 4,
            Categories =
            [
                new CategoryScoreBreakdown
                {
                    GroupId = "top-n",
                    QuestionId = "other-category"
                }
            ]
        };

        var state = TopNResultsDisplayState.Resolve(group, group.Questions[0], score);

        Assert.True(state.HasResultsImported);
        Assert.False(state.IsActiveCategoryScored);
        Assert.Null(state.ActiveBreakdown);
        Assert.Null(state.ActiveSheetResults);
    }

    private static PredictionGroup CreateGroup()
    {
        return new PredictionGroup
        {
            Id = "top-n",
            Title = "Top N",
            Questions =
            [
                new PredictionQuestion
                {
                    Id = "women-52",
                    Type = PredictionQuestionType.CategoryPodium,
                    Title = "Women 52",
                    Order = 1,
                    Constraints = new PredictionQuestionConstraints { ExactSelections = 3 }
                },
                new PredictionQuestion
                {
                    Id = "women-57",
                    Type = PredictionQuestionType.AthleteRanking,
                    Title = "Women 57",
                    Order = 2,
                    Constraints = new PredictionQuestionConstraints { MaxSelections = 5 }
                },
                new PredictionQuestion
                {
                    Id = "attendance",
                    Type = PredictionQuestionType.YesNo,
                    Title = "Attendance",
                    Order = 3
                }
            ]
        };
    }
}
