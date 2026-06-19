using TotalCall.Client.Components.Predictions.TopN;
using TotalCall.Core.Domain.Athletes;
using TotalCall.Core.Domain.Predictions;

namespace TotalCall.Tests.Components.Predictions.TopN;

public sealed class TopNSlotViewBuilderTests
{
    [Fact]
    public void Build_MapsPlacementAthleteModeContextAndHistoryToSlotViews()
    {
        var placements = new[]
        {
            Pick("athlete-a", 1, totalKg: 300m, isScored: true, isAutoSeeded: false),
            Pick("athlete-b", 2, squatKg: 120m, benchKg: 90m, deadliftKg: 170m, isScored: false, isAutoSeeded: true)
        };

        var views = TopNSlotViewBuilder.Build(
            placements,
            [
                Athlete("athlete-a", "Athlete A", seedTotalKg: 305m),
                Athlete("athlete-b", "Athlete B", seedTotalKg: null, status: AthleteStatus.Withdrawn)
            ],
            new Dictionary<int, string> { [1] = TopNEntryMode.Total },
            TopNEntryMode.Lifts,
            TopNContextSelectionState.Empty.Open("ATHLETE-B"),
            new Dictionary<string, AthleteHistoryEntry?>
            {
                ["athlete-a"] = History(lastTotalKg: 295m, bestTotalKg: 310m)
            },
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "ATHLETE-B" });

        Assert.Collection(
            views,
            first =>
            {
                Assert.Equal(1, first.Position);
                Assert.Equal("athlete-a", first.Athlete!.Id);
                Assert.Same(placements[0], first.Placement);
                Assert.Equal(TopNEntryMode.Total, first.Mode);
                Assert.False(first.IsContextActive);
                Assert.True(first.IsScored);
                Assert.False(first.IsAutoSeeded);
                Assert.False(first.IsWithdrawn);
                Assert.False(first.CanMoveUp);
                Assert.True(first.CanMoveDown);
                Assert.True(first.CanUseNominated);
                Assert.True(first.CanUseLast);
                Assert.True(first.CanUseBest);
                Assert.False(first.IsHistoryLoading);
                Assert.Equal(305m, first.NominatedTotalKg);
                Assert.Equal(295m, first.LastTotalKg);
                Assert.Equal(310m, first.BestTotalKg);
            },
            second =>
            {
                Assert.Equal(2, second.Position);
                Assert.Equal("athlete-b", second.Athlete!.Id);
                Assert.Equal(TopNEntryMode.Lifts, second.Mode);
                Assert.True(second.IsContextActive);
                Assert.False(second.IsScored);
                Assert.True(second.IsAutoSeeded);
                Assert.True(second.IsWithdrawn);
                Assert.True(second.CanMoveUp);
                Assert.False(second.CanMoveDown);
                Assert.False(second.CanUseNominated);
                Assert.False(second.CanUseLast);
                Assert.False(second.CanUseBest);
                Assert.True(second.IsHistoryLoading);
                Assert.Null(second.NominatedTotalKg);
                Assert.Null(second.LastTotalKg);
                Assert.Null(second.BestTotalKg);
            });
    }

    [Fact]
    public void Build_ThrowsWhenPlacementReferencesUnknownAthlete()
    {
        var placements = new[] { Pick("missing", 1, totalKg: 300m) };

        Assert.Throws<InvalidOperationException>(() => TopNSlotViewBuilder.Build(
            placements,
            [],
            new Dictionary<int, string>(),
            TopNEntryMode.Total,
            TopNContextSelectionState.Empty,
            new Dictionary<string, AthleteHistoryEntry?>(),
            new HashSet<string>()));
    }

    private static Athlete Athlete(
        string id,
        string displayName,
        decimal? seedTotalKg,
        AthleteStatus status = AthleteStatus.Active)
    {
        return new Athlete
        {
            Id = id,
            DisplayName = displayName,
            SeedTotalKg = seedTotalKg,
            Status = status
        };
    }

    private static AthleteHistoryEntry History(decimal lastTotalKg, decimal bestTotalKg)
    {
        return new AthleteHistoryEntry
        {
            LastResult = new AthleteLastResult { TotalKg = lastTotalKg },
            Bests = new AthleteLiftBests { TotalKg = bestTotalKg }
        };
    }

    private static AthletePlacementPick Pick(
        string athleteId,
        int position,
        decimal? totalKg = null,
        decimal? squatKg = null,
        decimal? benchKg = null,
        decimal? deadliftKg = null,
        bool isScored = true,
        bool isAutoSeeded = false)
    {
        return new AthletePlacementPick
        {
            AthleteId = athleteId,
            Position = position,
            PredictedSquatKg = squatKg,
            PredictedBenchKg = benchKg,
            PredictedDeadliftKg = deadliftKg,
            PredictedTotalKg = totalKg ?? (squatKg ?? 0m) + (benchKg ?? 0m) + (deadliftKg ?? 0m),
            IsScored = isScored,
            IsAutoSeeded = isAutoSeeded
        };
    }
}
