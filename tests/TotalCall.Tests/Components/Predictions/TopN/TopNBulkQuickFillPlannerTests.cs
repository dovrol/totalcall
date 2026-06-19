using TotalCall.Client.Components.Predictions.TopN;
using TotalCall.Core.Domain.Athletes;
using TotalCall.Core.Domain.Predictions;

namespace TotalCall.Tests.Components.Predictions.TopN;

public sealed class TopNBulkQuickFillPlannerTests
{
    [Fact]
    public void GetDefaultPositions_ReturnsAutoSeededPositions()
    {
        var positions = TopNBulkQuickFillPlanner.GetDefaultPositions(
            [
                Pick("athlete-a", 1, isAutoSeeded: true),
                Pick("athlete-b", 2, isAutoSeeded: false),
                Pick("athlete-c", 3, isAutoSeeded: true)
            ]);

        Assert.Equal([1, 3], positions);
    }

    [Fact]
    public void FillDefaultPositions_FillsOnlyDefaultPositionsFromNominatedSource()
    {
        var placements = new[]
        {
            Pick("athlete-a", 1, totalKg: 300m, isAutoSeeded: true),
            Pick("athlete-b", 2, totalKg: 400m, isAutoSeeded: false)
        };

        var result = TopNBulkQuickFillPlanner.FillDefaultPositions(
            placements,
            [1],
            [
                Athlete("athlete-a", seedTotalKg: 305m),
                Athlete("athlete-b", seedTotalKg: 405m)
            ],
            new Dictionary<string, AthleteHistoryEntry?>(),
            TopNQuickFillKind.Nominated);

        Assert.True(result.Changed);
        Assert.Equal(TopNEntryMode.Total, result.ModeByPosition[1]);
        Assert.Collection(
            result.Placements,
            first =>
            {
                Assert.Equal("athlete-a", first.AthleteId);
                Assert.Equal(305m, first.PredictedTotalKg);
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
    public void FillDefaultPositions_FillsFromHistoryAndUsesLiftModeWhenLiftValuesExist()
    {
        var result = TopNBulkQuickFillPlanner.FillDefaultPositions(
            [Pick("athlete-a", 1, totalKg: 300m, isAutoSeeded: true)],
            [1],
            [Athlete("athlete-a", seedTotalKg: 305m)],
            new Dictionary<string, AthleteHistoryEntry?>
            {
                ["athlete-a"] = new()
                {
                    LastResult = new AthleteLastResult
                    {
                        SquatKg = 100m,
                        BenchKg = 80m,
                        DeadliftKg = 130m,
                        TotalKg = 310m
                    }
                }
            },
            TopNQuickFillKind.Last);

        Assert.True(result.Changed);
        Assert.Equal(TopNEntryMode.Lifts, result.ModeByPosition[1]);
        Assert.Collection(
            result.Placements,
            placement =>
            {
                Assert.Equal(100m, placement.PredictedSquatKg);
                Assert.Equal(80m, placement.PredictedBenchKg);
                Assert.Equal(130m, placement.PredictedDeadliftKg);
                Assert.Equal(310m, placement.PredictedTotalKg);
                Assert.False(placement.IsAutoSeeded);
            });
    }

    [Fact]
    public void FillDefaultPositions_ReturnsUnchangedWhenSourceCannotFill()
    {
        var placements = new[] { Pick("athlete-a", 1, totalKg: 300m, isAutoSeeded: true) };

        var result = TopNBulkQuickFillPlanner.FillDefaultPositions(
            placements,
            [1],
            [Athlete("athlete-a", seedTotalKg: null)],
            new Dictionary<string, AthleteHistoryEntry?>(),
            TopNQuickFillKind.Nominated);

        Assert.False(result.Changed);
        Assert.Empty(result.ModeByPosition);
        Assert.Same(placements[0], result.Placements[0]);
    }

    [Fact]
    public void FillDefaultPositions_ThrowsWhenPlacementReferencesUnknownAthlete()
    {
        Assert.Throws<InvalidOperationException>(() => TopNBulkQuickFillPlanner.FillDefaultPositions(
            [Pick("missing", 1, totalKg: 300m, isAutoSeeded: true)],
            [1],
            [],
            new Dictionary<string, AthleteHistoryEntry?>(),
            TopNQuickFillKind.Nominated));
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
