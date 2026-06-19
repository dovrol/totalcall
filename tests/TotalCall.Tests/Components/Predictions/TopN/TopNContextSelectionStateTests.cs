using TotalCall.Client.Components.Predictions.TopN;
using TotalCall.Core.Domain.Athletes;
using TotalCall.Core.Domain.Predictions;

namespace TotalCall.Tests.Components.Predictions.TopN;

public sealed class TopNContextSelectionStateTests
{
    [Fact]
    public void Open_WithAthleteId_OpensContext()
    {
        var state = TopNContextSelectionState.Empty.Open("athlete-a");

        Assert.True(state.IsOpen);
        Assert.Equal("athlete-a", state.SelectedAthleteId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Open_WithBlankAthleteId_ReturnsEmptyState(string? athleteId)
    {
        var state = TopNContextSelectionState.Empty.Open(athleteId);

        Assert.Same(TopNContextSelectionState.Empty, state);
        Assert.False(state.IsOpen);
    }

    [Fact]
    public void EnsureSelectable_KeepsSelectedAthleteWhenAvailable()
    {
        var state = TopNContextSelectionState.Empty.Open("athlete-a");

        var next = state.EnsureSelectable(IsSelectable);

        Assert.Same(state, next);
    }

    [Fact]
    public void EnsureSelectable_ClosesWhenSelectedAthleteIsNotAvailable()
    {
        var state = TopNContextSelectionState.Empty.Open("unknown");

        var next = state.EnsureSelectable(IsSelectable);

        Assert.Same(TopNContextSelectionState.Empty, next);
    }

    [Fact]
    public void Close_ReturnsEmptyState()
    {
        var state = TopNContextSelectionState.Empty.Open("athlete-a");

        var next = state.Close();

        Assert.Same(TopNContextSelectionState.Empty, next);
        Assert.False(next.IsOpen);
    }

    [Fact]
    public void IsSelected_MatchesCaseInsensitiveAthleteId()
    {
        var state = TopNContextSelectionState.Empty.Open("ATHLETE-A");

        Assert.True(state.IsSelected("athlete-a"));
        Assert.False(state.IsSelected("athlete-b"));
        Assert.False(state.IsSelected(null));
    }

    [Fact]
    public void SelectedPlacementValues_ReturnRankAndPredictedTotal()
    {
        var state = TopNContextSelectionState.Empty.Open("athlete-b");

        var placements = new[]
        {
            Pick("athlete-a", 1, totalKg: 300m),
            Pick("ATHLETE-B", 2, totalKg: 400m)
        };

        Assert.Equal(2, state.GetSelectedRank(placements));
        Assert.Equal(400m, state.GetSelectedPredictedTotal(placements));
    }

    [Fact]
    public void SelectedPlacementValues_ReturnNullWhenSelectedAthleteIsMissing()
    {
        var state = TopNContextSelectionState.Empty.Open("missing");

        var placements = new[] { Pick("athlete-a", 1, totalKg: 300m) };

        Assert.Null(state.GetSelectedRank(placements));
        Assert.Null(state.GetSelectedPredictedTotal(placements));
    }

    [Fact]
    public void BuildSelectedAthletesByPosition_MapsKnownAthletesByPlacementPosition()
    {
        var state = TopNContextSelectionState.Empty;

        var selected = state.BuildSelectedAthletesByPosition(
            [
                Pick("athlete-a", 2, totalKg: 300m),
                Pick("missing", 1, totalKg: 999m),
                Pick("ATHLETE-B", 3, totalKg: 400m)
            ],
            [
                Athlete("athlete-a", "Athlete A"),
                Athlete("athlete-b", "Athlete B")
            ]);

        Assert.Equal([2, 3], selected.Keys.OrderBy(key => key));
        Assert.Equal("Athlete A", selected[2].DisplayName);
        Assert.Equal("Athlete B", selected[3].DisplayName);
    }

    private static bool IsSelectable(string athleteId)
    {
        return athleteId is "athlete-a" or "athlete-b";
    }

    private static AthletePlacementPick Pick(string athleteId, int position, decimal totalKg)
    {
        return new AthletePlacementPick
        {
            AthleteId = athleteId,
            Position = position,
            PredictedTotalKg = totalKg
        };
    }

    private static Athlete Athlete(string id, string displayName)
    {
        return new Athlete
        {
            Id = id,
            DisplayName = displayName
        };
    }
}
