using TotalCall.Client.Components.Predictions.TopN;
using TotalCall.Core.Domain.Predictions;

namespace TotalCall.Tests.Components.Predictions.TopN;

public sealed class TopNSortPlannerTests
{
    [Fact]
    public void SortByPredictedTotal_ReturnsNullForEmptyOrUnscoredField()
    {
        Assert.Null(TopNSortPlanner.SortByPredictedTotal([], scoredPositionsCount: 3));
        Assert.Null(TopNSortPlanner.SortByPredictedTotal(
            [Pick("athlete-a", 1, totalKg: null), Pick("athlete-b", 2, totalKg: null)],
            scoredPositionsCount: 3));
    }

    [Fact]
    public void SortByPredictedTotal_SortsDescendingWithNullsLastAndStableTies()
    {
        var sorted = TopNSortPlanner.SortByPredictedTotal(
            [
                Pick("athlete-a", 4, totalKg: 200m),
                Pick("athlete-b", 3, totalKg: null),
                Pick("athlete-c", 2, totalKg: 300m),
                Pick("athlete-d", 1, totalKg: 300m)
            ],
            scoredPositionsCount: 2);

        Assert.NotNull(sorted);
        Assert.Collection(
            sorted,
            first =>
            {
                Assert.Equal("athlete-c", first.AthleteId);
                Assert.Equal(1, first.Position);
                Assert.True(first.IsScored);
            },
            second =>
            {
                Assert.Equal("athlete-d", second.AthleteId);
                Assert.Equal(2, second.Position);
                Assert.True(second.IsScored);
            },
            third =>
            {
                Assert.Equal("athlete-a", third.AthleteId);
                Assert.Equal(3, third.Position);
                Assert.False(third.IsScored);
            },
            fourth =>
            {
                Assert.Equal("athlete-b", fourth.AthleteId);
                Assert.Equal(4, fourth.Position);
                Assert.False(fourth.IsScored);
            });
    }

    private static AthletePlacementPick Pick(string athleteId, int position, decimal? totalKg)
    {
        return new AthletePlacementPick
        {
            AthleteId = athleteId,
            Position = position,
            PredictedTotalKg = totalKg
        };
    }
}
