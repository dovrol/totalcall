using System.Globalization;
using TotalCall.Client.Components.Predictions.TopN;
using TotalCall.Core.Domain.Predictions;

namespace TotalCall.Tests.Components.Predictions.TopN;

public sealed class TopNSheetEditPlannerTests
{
    [Fact]
    public void ApplyEdit_WithInvalidValue_ReturnsInvalidValueStatus()
    {
        var plan = TopNSheetEditPlanner.ApplyEdit(
            [Pick("athlete-a", 1, totalKg: 300m)],
            new TopNSheetEdit(1, TopNSheetField.Total, "nope"),
            CultureInfo.InvariantCulture);

        Assert.Equal(TopNSheetEditPlanStatus.InvalidValue, plan.Status);
        Assert.Empty(plan.Placements);
    }

    [Fact]
    public void ApplyEdit_WithMissingPosition_IsIgnored()
    {
        var plan = TopNSheetEditPlanner.ApplyEdit(
            [Pick("athlete-a", 1, totalKg: 300m)],
            new TopNSheetEdit(2, TopNSheetField.Total, "320"),
            CultureInfo.InvariantCulture);

        Assert.Equal(TopNSheetEditPlanStatus.Ignored, plan.Status);
        Assert.Empty(plan.Placements);
    }

    [Fact]
    public void ApplyEdit_UpdatesTargetPlacementOnly()
    {
        var placements = new[]
        {
            Pick("athlete-a", 1, totalKg: 300m, isAutoSeeded: true),
            Pick("athlete-b", 2, totalKg: 400m)
        };

        var plan = TopNSheetEditPlanner.ApplyEdit(
            placements,
            new TopNSheetEdit(1, TopNSheetField.Total, "320"),
            CultureInfo.InvariantCulture);

        Assert.Equal(TopNSheetEditPlanStatus.Applied, plan.Status);
        Assert.Collection(
            plan.Placements,
            first =>
            {
                Assert.Equal("athlete-a", first.AthleteId);
                Assert.Equal(320m, first.PredictedTotalKg);
                Assert.Null(first.PredictedSquatKg);
                Assert.Null(first.PredictedBenchKg);
                Assert.Null(first.PredictedDeadliftKg);
                Assert.False(first.IsAutoSeeded);
            },
            second =>
            {
                Assert.Same(placements[1], second);
            });
    }

    [Fact]
    public void ApplyNudge_WithInvalidDelta_IsIgnored()
    {
        var plan = TopNSheetEditPlanner.ApplyNudge(
            [Pick("athlete-a", 1, totalKg: 300m)],
            new TopNSheetEdit(1, TopNSheetField.Total, "bad"));

        Assert.Equal(TopNSheetEditPlanStatus.Ignored, plan.Status);
        Assert.Empty(plan.Placements);
    }

    [Fact]
    public void ApplyNudge_WithMissingPosition_IsIgnored()
    {
        var plan = TopNSheetEditPlanner.ApplyNudge(
            [Pick("athlete-a", 1, totalKg: 300m)],
            new TopNSheetEdit(2, TopNSheetField.Total, "5"));

        Assert.Equal(TopNSheetEditPlanStatus.Ignored, plan.Status);
        Assert.Empty(plan.Placements);
    }

    [Fact]
    public void ApplyNudge_UpdatesTargetPlacementOnly()
    {
        var placements = new[]
        {
            Pick("athlete-a", 1, totalKg: 300m),
            Pick("athlete-b", 2, totalKg: 400m)
        };

        var plan = TopNSheetEditPlanner.ApplyNudge(
            placements,
            new TopNSheetEdit(1, TopNSheetField.Total, "5"));

        Assert.Equal(TopNSheetEditPlanStatus.Applied, plan.Status);
        Assert.Collection(
            plan.Placements,
            first =>
            {
                Assert.Equal("athlete-a", first.AthleteId);
                Assert.Equal(305m, first.PredictedTotalKg);
                Assert.False(first.IsAutoSeeded);
            },
            second =>
            {
                Assert.Same(placements[1], second);
            });
    }

    private static AthletePlacementPick Pick(
        string athleteId,
        int position,
        decimal? totalKg = null,
        bool isAutoSeeded = false)
    {
        return new AthletePlacementPick
        {
            AthleteId = athleteId,
            Position = position,
            PredictedTotalKg = totalKg,
            IsAutoSeeded = isAutoSeeded
        };
    }
}
