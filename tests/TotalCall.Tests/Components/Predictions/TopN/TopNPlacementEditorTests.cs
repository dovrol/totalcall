using System.Globalization;
using TotalCall.Client.Components.Predictions.TopN;
using TotalCall.Core.Domain.Predictions;

namespace TotalCall.Tests.Components.Predictions.TopN;

public sealed class TopNPlacementEditorTests
{
    [Fact]
    public void ResolveEntryMode_UsesExplicitModeBeforePlacementShape()
    {
        var modeByPosition = new Dictionary<int, string>
        {
            [1] = TopNEntryMode.Total
        };

        var mode = TopNPlacementEditor.ResolveEntryMode(
            modeByPosition,
            1,
            Pick(squat: 100, bench: 80, deadlift: 140),
            TopNEntryMode.Lifts);

        Assert.Equal(TopNEntryMode.Total, mode);
    }

    [Fact]
    public void ResolveEntryMode_UsesLiftModeWhenPlacementHasLiftValues()
    {
        var mode = TopNPlacementEditor.ResolveEntryMode(
            new Dictionary<int, string>(),
            1,
            Pick(squat: 100, bench: null, deadlift: null),
            TopNEntryMode.Total);

        Assert.Equal(TopNEntryMode.Lifts, mode);
    }

    [Fact]
    public void ApplyEdit_TotalClearsLiftValuesAndMarksManual()
    {
        var edited = TopNPlacementEditor.ApplyEdit(
            Pick(squat: 100, bench: 80, deadlift: 140, isAutoSeeded: true),
            TopNSheetField.Total,
            330m);

        Assert.Null(edited.PredictedSquatKg);
        Assert.Null(edited.PredictedBenchKg);
        Assert.Null(edited.PredictedDeadliftKg);
        Assert.Equal(330m, edited.PredictedTotalKg);
        Assert.False(edited.IsAutoSeeded);
    }

    [Fact]
    public void ApplyEdit_LiftFieldRecalculatesTotal()
    {
        var edited = TopNPlacementEditor.ApplyEdit(
            Pick(squat: 100, bench: 80, deadlift: 140),
            TopNSheetField.Bench,
            90m);

        Assert.Equal(100m, edited.PredictedSquatKg);
        Assert.Equal(90m, edited.PredictedBenchKg);
        Assert.Equal(140m, edited.PredictedDeadliftKg);
        Assert.Equal(330m, edited.PredictedTotalKg);
    }

    [Fact]
    public void ApplyNudge_ClampsToValidRangeAndClearsZero()
    {
        var lowered = TopNPlacementEditor.ApplyNudge(
            Pick(total: 100m),
            TopNSheetField.Total,
            -150m);
        var capped = TopNPlacementEditor.ApplyNudge(
            Pick(total: 1495m),
            TopNSheetField.Total,
            10m);

        Assert.Null(lowered.PredictedTotalKg);
        Assert.Equal(1500m, capped.PredictedTotalKg);
    }

    [Fact]
    public void ApplyNudge_LiftFieldRecalculatesTotal()
    {
        var nudged = TopNPlacementEditor.ApplyNudge(
            Pick(squat: 100, bench: 80, deadlift: 140),
            TopNSheetField.Bench,
            10m);

        Assert.Equal(90m, nudged.PredictedBenchKg);
        Assert.Equal(330m, nudged.PredictedTotalKg);
    }

    [Fact]
    public void TryReadOptionalPositiveDecimal_ParsesCultureAndInvariantValues()
    {
        Assert.True(TopNPlacementEditor.TryReadOptionalPositiveDecimal("123,5", new CultureInfo("pl-PL"), out var local));
        Assert.True(TopNPlacementEditor.TryReadOptionalPositiveDecimal("123.5", new CultureInfo("pl-PL"), out var invariant));
        Assert.True(TopNPlacementEditor.TryReadOptionalPositiveDecimal(" ", CultureInfo.InvariantCulture, out var empty));

        Assert.Equal(123.5m, local);
        Assert.Equal(123.5m, invariant);
        Assert.Null(empty);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("1500.1")]
    [InlineData("not-a-number")]
    public void TryReadOptionalPositiveDecimal_RejectsInvalidValues(string value)
    {
        Assert.False(TopNPlacementEditor.TryReadOptionalPositiveDecimal(
            value,
            CultureInfo.InvariantCulture,
            out _));
    }

    private static AthletePlacementPick Pick(
        decimal? total = null,
        decimal? squat = null,
        decimal? bench = null,
        decimal? deadlift = null,
        bool isAutoSeeded = false)
    {
        return new AthletePlacementPick
        {
            AthleteId = "athlete-a",
            Position = 1,
            PredictedTotalKg = total ?? (squat ?? 0m) + (bench ?? 0m) + (deadlift ?? 0m),
            PredictedSquatKg = squat,
            PredictedBenchKg = bench,
            PredictedDeadliftKg = deadlift,
            IsAutoSeeded = isAutoSeeded
        };
    }
}
