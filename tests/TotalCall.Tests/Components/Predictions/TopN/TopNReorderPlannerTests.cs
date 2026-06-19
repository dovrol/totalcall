using TotalCall.Client.Components.Predictions.TopN;
using TotalCall.Core.Domain.Predictions;

namespace TotalCall.Tests.Components.Predictions.TopN;

public sealed class TopNReorderPlannerTests
{
    [Fact]
    public void MoveToPosition_MovesRowDownAndRenumbersScoredSlots()
    {
        var plan = TopNReorderPlanner.MoveToPosition(
            [Pick("athlete-a", 1), Pick("athlete-b", 2), Pick("athlete-c", 3)],
            from: 1,
            to: 3,
            scoredPositionsCount: 2);

        Assert.NotNull(plan);
        Assert.Equal(["athlete-b", "athlete-c", "athlete-a"], plan.AfterOrder.Select(placement => placement.AthleteId));
        Assert.Equal([1, 2, 3], plan.AfterOrder.Select(placement => placement.Position));
        Assert.Equal([true, true, false], plan.AfterOrder.Select(placement => placement.IsScored));
    }

    [Fact]
    public void MoveToPosition_MovesRowUp()
    {
        var plan = TopNReorderPlanner.MoveToPosition(
            [Pick("athlete-a", 1), Pick("athlete-b", 2), Pick("athlete-c", 3)],
            from: 3,
            to: 1,
            scoredPositionsCount: 2);

        Assert.NotNull(plan);
        Assert.Equal(["athlete-c", "athlete-a", "athlete-b"], plan.AfterOrder.Select(placement => placement.AthleteId));
    }

    [Fact]
    public void MoveToPosition_ClampsTargetPosition()
    {
        var plan = TopNReorderPlanner.MoveToPosition(
            [Pick("athlete-a", 1), Pick("athlete-b", 2), Pick("athlete-c", 3)],
            from: 2,
            to: 99,
            scoredPositionsCount: 2);

        Assert.NotNull(plan);
        Assert.Equal(["athlete-a", "athlete-c", "athlete-b"], plan.AfterOrder.Select(placement => placement.AthleteId));
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(9, 1)]
    public void MoveToPosition_ReturnsNullForNoOpOrMissingSource(int from, int to)
    {
        var plan = TopNReorderPlanner.MoveToPosition(
            [Pick("athlete-a", 1), Pick("athlete-b", 2)],
            from,
            to,
            scoredPositionsCount: 1);

        Assert.Null(plan);
    }

    [Fact]
    public void RemapModesAfterReorder_KeepsModesAttachedToAthletes()
    {
        var plan = TopNReorderPlanner.MoveToPosition(
            [Pick("athlete-a", 1), Pick("athlete-b", 2), Pick("athlete-c", 3)],
            from: 1,
            to: 3,
            scoredPositionsCount: 2);
        var modes = new Dictionary<int, string>
        {
            [1] = TopNEntryMode.Lifts,
            [3] = TopNEntryMode.Total
        };

        var remapped = TopNReorderPlanner.RemapModesAfterReorder(
            modes,
            plan!.BeforeOrder,
            plan.AfterOrder);

        Assert.Equal(2, remapped.Count);
        Assert.Equal(TopNEntryMode.Total, remapped[2]);
        Assert.Equal(TopNEntryMode.Lifts, remapped[3]);
    }

    private static AthletePlacementPick Pick(string athleteId, int position)
    {
        return new AthletePlacementPick
        {
            AthleteId = athleteId,
            Position = position,
            PredictedTotalKg = 100m + position
        };
    }
}
