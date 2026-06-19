using TotalCall.Client.Components.Predictions.TopN;
using TotalCall.Core.Domain.Athletes;
using TotalCall.Core.Domain.Predictions;

namespace TotalCall.Tests.Components.Predictions.TopN;

public sealed class TopNQuickFillBuilderTests
{
    [Fact]
    public void BuildFromSource_NominatedUsesAthleteSeedTotal()
    {
        var filled = TopNQuickFillBuilder.BuildFromSource(
            Pick(squat: 100, bench: 80, deadlift: 140, isAutoSeeded: true),
            CreateAthlete(seedTotalKg: 350m),
            history: null,
            TopNQuickFillKind.Nominated);

        Assert.NotNull(filled);
        Assert.Null(filled.PredictedSquatKg);
        Assert.Null(filled.PredictedBenchKg);
        Assert.Null(filled.PredictedDeadliftKg);
        Assert.Equal(350m, filled.PredictedTotalKg);
        Assert.False(filled.IsAutoSeeded);
    }

    [Fact]
    public void BuildFromSource_NominatedWithoutSeedReturnsNull()
    {
        var filled = TopNQuickFillBuilder.BuildFromSource(
            Pick(),
            CreateAthlete(seedTotalKg: null),
            history: null,
            TopNQuickFillKind.Nominated);

        Assert.Null(filled);
    }

    [Fact]
    public void BuildFromSource_LastResultUsesLiftBreakdown()
    {
        var filled = TopNQuickFillBuilder.BuildFromSource(
            Pick(isAutoSeeded: true),
            CreateAthlete(seedTotalKg: 350m),
            new AthleteHistoryEntry
            {
                LastResult = new AthleteLastResult
                {
                    SquatKg = 110m,
                    BenchKg = 90m,
                    DeadliftKg = 150m,
                    TotalKg = 350m
                }
            },
            TopNQuickFillKind.Last);

        Assert.NotNull(filled);
        Assert.Equal(110m, filled.PredictedSquatKg);
        Assert.Equal(90m, filled.PredictedBenchKg);
        Assert.Equal(150m, filled.PredictedDeadliftKg);
        Assert.Equal(350m, filled.PredictedTotalKg);
        Assert.False(filled.IsAutoSeeded);
    }

    [Fact]
    public void BuildFromSource_BestResultCanUseTotalOnly()
    {
        var filled = TopNQuickFillBuilder.BuildFromSource(
            Pick(isAutoSeeded: true),
            CreateAthlete(seedTotalKg: 350m),
            new AthleteHistoryEntry
            {
                Bests = new AthleteLiftBests
                {
                    TotalKg = 365m
                }
            },
            TopNQuickFillKind.Best);

        Assert.NotNull(filled);
        Assert.Null(filled.PredictedSquatKg);
        Assert.Null(filled.PredictedBenchKg);
        Assert.Null(filled.PredictedDeadliftKg);
        Assert.Equal(365m, filled.PredictedTotalKg);
        Assert.False(filled.IsAutoSeeded);
    }

    [Theory]
    [InlineData(TopNQuickFillKind.Last)]
    [InlineData(TopNQuickFillKind.Best)]
    [InlineData("unknown")]
    public void BuildFromSource_WhenRequestedDataIsUnavailable_ReturnsNull(string kind)
    {
        var filled = TopNQuickFillBuilder.BuildFromSource(
            Pick(),
            CreateAthlete(seedTotalKg: 350m),
            new AthleteHistoryEntry(),
            kind);

        Assert.Null(filled);
    }

    private static Athlete CreateAthlete(decimal? seedTotalKg)
    {
        return new Athlete
        {
            Id = "athlete-a",
            DisplayName = "Athlete A",
            SeedTotalKg = seedTotalKg
        };
    }

    private static AthletePlacementPick Pick(
        decimal? squat = null,
        decimal? bench = null,
        decimal? deadlift = null,
        bool isAutoSeeded = false)
    {
        return new AthletePlacementPick
        {
            AthleteId = "athlete-a",
            Position = 1,
            PredictedSquatKg = squat,
            PredictedBenchKg = bench,
            PredictedDeadliftKg = deadlift,
            PredictedTotalKg = squat is not null || bench is not null || deadlift is not null
                ? (squat ?? 0m) + (bench ?? 0m) + (deadlift ?? 0m)
                : null,
            IsAutoSeeded = isAutoSeeded
        };
    }
}
