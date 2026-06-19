using TotalCall.Client.Components.Predictions.TopN;
using TotalCall.Core.Domain.Athletes;
using TotalCall.Core.Domain.Predictions;

namespace TotalCall.Tests.Components.Predictions.TopN;

public sealed class TopNAthleteResetPlannerTests
{
    [Fact]
    public void ResetAthlete_ReturnsNullWhenPositionIsMissing()
    {
        var result = TopNAthleteResetPlanner.ResetAthlete(
            [Pick("athlete-a", 1, totalKg: 300m)],
            [Athlete("athlete-a", seedTotalKg: 305m)],
            position: 2);

        Assert.Null(result);
    }

    [Fact]
    public void ResetAthlete_ResetsSelectedPlacementToNominatedTotal()
    {
        var placements = new[]
        {
            Pick("athlete-a", 1, squatKg: 100m, benchKg: 80m, deadliftKg: 130m, isAutoSeeded: false),
            Pick("athlete-b", 2, totalKg: 400m, isAutoSeeded: false)
        };

        var result = TopNAthleteResetPlanner.ResetAthlete(
            placements,
            [
                Athlete("athlete-a", seedTotalKg: 305m),
                Athlete("athlete-b", seedTotalKg: 405m)
            ],
            position: 1);

        Assert.NotNull(result);
        Assert.Equal("athlete-a", result.Athlete.Id);
        Assert.Collection(
            result.Placements,
            first =>
            {
                Assert.Equal("athlete-a", first.AthleteId);
                Assert.Null(first.PredictedSquatKg);
                Assert.Null(first.PredictedBenchKg);
                Assert.Null(first.PredictedDeadliftKg);
                Assert.Equal(305m, first.PredictedTotalKg);
                Assert.True(first.IsAutoSeeded);
            },
            second =>
            {
                Assert.Same(placements[1], second);
            });
    }

    [Fact]
    public void ResetAthlete_ThrowsWhenPlacementReferencesUnknownAthlete()
    {
        Assert.Throws<InvalidOperationException>(() => TopNAthleteResetPlanner.ResetAthlete(
            [Pick("missing", 1, totalKg: 300m)],
            [],
            position: 1));
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
        decimal? deadliftKg = null,
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
            IsAutoSeeded = isAutoSeeded
        };
    }
}
