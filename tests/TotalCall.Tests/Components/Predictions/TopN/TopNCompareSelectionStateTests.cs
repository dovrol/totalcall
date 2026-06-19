using TotalCall.Client.Components.Predictions.TopN;
using TotalCall.Core.Domain.Athletes;
using TotalCall.Core.Domain.Predictions;

namespace TotalCall.Tests.Components.Predictions.TopN;

public sealed class TopNCompareSelectionStateTests
{
    [Fact]
    public void Start_WithSelectableAthlete_ActivatesAndPreselectsAthlete()
    {
        var state = TopNCompareSelectionState.Empty.Start("athlete-a", IsSelectable);

        Assert.True(state.IsActive);
        Assert.Contains("athlete-a", state.SelectedAthleteIds);
    }

    [Fact]
    public void Start_WithInvalidAthlete_ActivatesWithoutSelection()
    {
        var state = TopNCompareSelectionState.Empty.Start("unknown", IsSelectable);

        Assert.True(state.IsActive);
        Assert.Empty(state.SelectedAthleteIds);
    }

    [Fact]
    public void Toggle_WhenInactive_StartsSelectionWithAthlete()
    {
        var (state, result) = TopNCompareSelectionState.Empty.Toggle("athlete-a", IsSelectable);

        Assert.Equal(TopNCompareSelectionToggleResult.Started, result);
        Assert.True(state.IsActive);
        Assert.Contains("athlete-a", state.SelectedAthleteIds);
    }

    [Fact]
    public void Toggle_WhenActive_AddsAndRemovesAthletes()
    {
        var state = TopNCompareSelectionState.Empty.Start("athlete-a", IsSelectable);

        var (withSecond, addResult) = state.Toggle("athlete-b", IsSelectable);
        var (withoutFirst, removeResult) = withSecond.Toggle("athlete-a", IsSelectable);

        Assert.Equal(TopNCompareSelectionToggleResult.Added, addResult);
        Assert.Contains("athlete-a", withSecond.SelectedAthleteIds);
        Assert.Contains("athlete-b", withSecond.SelectedAthleteIds);
        Assert.Equal(TopNCompareSelectionToggleResult.Removed, removeResult);
        Assert.DoesNotContain("athlete-a", withoutFirst.SelectedAthleteIds);
        Assert.Contains("athlete-b", withoutFirst.SelectedAthleteIds);
    }

    [Fact]
    public void Toggle_WhenMaxSelectionsReached_KeepsExistingSelection()
    {
        var state = new TopNCompareSelectionState(
            true,
            ["athlete-a", "athlete-b", "athlete-c"]);

        var (next, result) = state.Toggle("athlete-d", IsSelectable);

        Assert.Equal(TopNCompareSelectionToggleResult.MaxReached, result);
        Assert.Same(state, next);
        Assert.Equal(3, next.SelectedAthleteIds.Count);
        Assert.DoesNotContain("athlete-d", next.SelectedAthleteIds);
    }

    [Theory]
    [InlineData("")]
    [InlineData("unknown")]
    public void Toggle_WithBlankOrInvalidAthlete_IsIgnored(string athleteId)
    {
        var state = TopNCompareSelectionState.Empty.Start("athlete-a", IsSelectable);

        var (next, result) = state.Toggle(athleteId, IsSelectable);

        Assert.Equal(TopNCompareSelectionToggleResult.Ignored, result);
        Assert.Same(state, next);
    }

    [Fact]
    public void Cancel_ReturnsInactiveEmptyState()
    {
        var state = TopNCompareSelectionState.Empty.Start("athlete-a", IsSelectable);

        var cancelled = state.Cancel();

        Assert.False(cancelled.IsActive);
        Assert.Empty(cancelled.SelectedAthleteIds);
    }

    [Fact]
    public void BuildSelectedSlots_MapsSelectedAthletesInPlacementOrder()
    {
        var state = new TopNCompareSelectionState(
            true,
            ["athlete-c", "athlete-a", "missing"]);

        var slots = state.BuildSelectedSlots(
            [
                Pick("athlete-a", 3, totalKg: 300m),
                Pick("athlete-b", 1, totalKg: 400m),
                Pick("athlete-c", 2, squatKg: 120m, benchKg: 90m, deadliftKg: 170m)
            ],
            [
                Athlete("athlete-a", "Athlete A", seedTotalKg: 305m),
                Athlete("athlete-b", "Athlete B", seedTotalKg: 405m),
                Athlete("athlete-c", "Athlete C", seedTotalKg: 380m)
            ]);

        Assert.Collection(
            slots,
            first =>
            {
                Assert.Equal(2, first.Position);
                Assert.Equal("athlete-c", first.AthleteId);
                Assert.Equal("Athlete C", first.AthleteName);
                Assert.Equal(380m, first.NominatedTotalKg);
                Assert.Equal(380m, first.PredictedTotalKg);
                Assert.Equal(120m, first.PredictedSquatKg);
                Assert.Equal(90m, first.PredictedBenchKg);
                Assert.Equal(170m, first.PredictedDeadliftKg);
            },
            second =>
            {
                Assert.Equal(3, second.Position);
                Assert.Equal("athlete-a", second.AthleteId);
                Assert.Equal(300m, second.PredictedTotalKg);
            });
    }

    private static bool IsSelectable(string athleteId)
    {
        return athleteId is "athlete-a" or "athlete-b" or "athlete-c" or "athlete-d";
    }

    private static Athlete Athlete(string id, string name, decimal seedTotalKg)
    {
        return new Athlete
        {
            Id = id,
            DisplayName = name,
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
