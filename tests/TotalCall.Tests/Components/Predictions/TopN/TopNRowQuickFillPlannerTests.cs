using TotalCall.Client.Components.Predictions.TopN;
using TotalCall.Core.Domain.Athletes;
using TotalCall.Core.Domain.Predictions;

namespace TotalCall.Tests.Components.Predictions.TopN;

public sealed class TopNRowQuickFillPlannerTests
{
    [Fact]
    public void ResolveTarget_ReturnsNullWhenPositionIsMissing()
    {
        var target = TopNRowQuickFillPlanner.ResolveTarget(
            [Pick("athlete-a", 1, totalKg: 300m)],
            [Athlete("athlete-a", seedTotalKg: 305m)],
            position: 2);

        Assert.Null(target);
    }

    [Fact]
    public void ResolveTarget_ReturnsNullWhenAthleteIsMissing()
    {
        var target = TopNRowQuickFillPlanner.ResolveTarget(
            [Pick("missing", 1, totalKg: 300m)],
            [],
            position: 1);

        Assert.Null(target);
    }

    [Fact]
    public void ResolveTarget_MapsPlacementAndAthleteCaseInsensitively()
    {
        var placement = Pick("ATHLETE-A", 1, totalKg: 300m);
        var athlete = Athlete("athlete-a", seedTotalKg: 305m);

        var target = TopNRowQuickFillPlanner.ResolveTarget(
            [placement],
            [athlete],
            position: 1);

        Assert.NotNull(target);
        Assert.Same(placement, target.Placement);
        Assert.Same(athlete, target.Athlete);
    }

    [Fact]
    public void Fill_FillsFromNominatedTotalAndReturnsTotalMode()
    {
        var placements = new[]
        {
            Pick("athlete-a", 1, squatKg: 100m, benchKg: 80m, deadliftKg: 130m),
            Pick("athlete-b", 2, totalKg: 400m)
        };

        var target = new TopNRowQuickFillTarget(
            placements[0],
            Athlete("athlete-a", seedTotalKg: 305m));

        var result = TopNRowQuickFillPlanner.Fill(
            placements,
            target,
            history: null,
            TopNQuickFillKind.Nominated);

        Assert.NotNull(result);
        Assert.Same(target.Athlete, result.Athlete);
        Assert.Equal(TopNEntryMode.Total, result.Mode);
        Assert.Collection(
            result.Placements,
            first =>
            {
                Assert.Equal("athlete-a", first.AthleteId);
                Assert.Null(first.PredictedSquatKg);
                Assert.Null(first.PredictedBenchKg);
                Assert.Null(first.PredictedDeadliftKg);
                Assert.Equal(305m, first.PredictedTotalKg);
                Assert.False(first.IsAutoSeeded);
            },
            second =>
            {
                Assert.Same(placements[1], second);
            });
    }

    [Fact]
    public void Fill_FillsFromHistoryAndReturnsLiftModeWhenLiftValuesExist()
    {
        var placement = Pick("athlete-a", 1, totalKg: 300m);
        var target = new TopNRowQuickFillTarget(
            placement,
            Athlete("athlete-a", seedTotalKg: 305m));

        var result = TopNRowQuickFillPlanner.Fill(
            [placement],
            target,
            new AthleteHistoryEntry
            {
                Bests = new AthleteLiftBests
                {
                    SquatKg = 110m,
                    BenchKg = 90m,
                    DeadliftKg = 140m,
                    TotalKg = 340m
                }
            },
            TopNQuickFillKind.Best);

        Assert.NotNull(result);
        Assert.Equal(TopNEntryMode.Lifts, result.Mode);
        Assert.Collection(
            result.Placements,
            filled =>
            {
                Assert.Equal(110m, filled.PredictedSquatKg);
                Assert.Equal(90m, filled.PredictedBenchKg);
                Assert.Equal(140m, filled.PredictedDeadliftKg);
                Assert.Equal(340m, filled.PredictedTotalKg);
                Assert.False(filled.IsAutoSeeded);
            });
    }

    [Fact]
    public void Fill_ReturnsNullWhenSourceCannotFill()
    {
        var placement = Pick("athlete-a", 1, totalKg: 300m);
        var target = new TopNRowQuickFillTarget(
            placement,
            Athlete("athlete-a", seedTotalKg: null));

        var result = TopNRowQuickFillPlanner.Fill(
            [placement],
            target,
            history: null,
            TopNQuickFillKind.Nominated);

        Assert.Null(result);
    }

    private static Athlete Athlete(string id, decimal? seedTotalKg)
    {
        return new Athlete
        {
            Id = id,
            DisplayName = id,
            SeedTotalKg = seedTotalKg
        };
    }

    private static AthletePlacementPick Pick(
        string athleteId,
        int position,
        decimal? totalKg = null,
        decimal? squatKg = null,
        decimal? benchKg = null,
        decimal? deadliftKg = null)
    {
        return new AthletePlacementPick
        {
            AthleteId = athleteId,
            Position = position,
            PredictedSquatKg = squatKg,
            PredictedBenchKg = benchKg,
            PredictedDeadliftKg = deadliftKg,
            PredictedTotalKg = totalKg ?? (squatKg ?? 0m) + (benchKg ?? 0m) + (deadliftKg ?? 0m)
        };
    }
}
