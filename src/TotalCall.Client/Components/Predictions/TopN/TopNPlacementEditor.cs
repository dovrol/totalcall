using System.Globalization;
using TotalCall.Core.Domain.Predictions;

namespace TotalCall.Client.Components.Predictions.TopN;

public static class TopNPlacementEditor
{
    public static string ResolveEntryMode(
        IReadOnlyDictionary<int, string> modeByPosition,
        int position,
        AthletePlacementPick? placement,
        string defaultMode)
    {
        if (modeByPosition.TryGetValue(position, out var mode))
        {
            return mode;
        }

        return HasLiftValues(placement) ? TopNEntryMode.Lifts : defaultMode;
    }

    public static AthletePlacementPick ApplyEdit(
        AthletePlacementPick placement,
        string field,
        decimal? value)
    {
        if (string.Equals(field, TopNSheetField.Total, StringComparison.OrdinalIgnoreCase))
        {
            return placement with
            {
                PredictedSquatKg = null,
                PredictedBenchKg = null,
                PredictedDeadliftKg = null,
                PredictedTotalKg = value,
                IsAutoSeeded = false
            };
        }

        var squat = string.Equals(field, TopNSheetField.Squat, StringComparison.OrdinalIgnoreCase) ? value : placement.PredictedSquatKg;
        var bench = string.Equals(field, TopNSheetField.Bench, StringComparison.OrdinalIgnoreCase) ? value : placement.PredictedBenchKg;
        var deadlift = string.Equals(field, TopNSheetField.Deadlift, StringComparison.OrdinalIgnoreCase) ? value : placement.PredictedDeadliftKg;

        var total = squat is not null || bench is not null || deadlift is not null
            ? (squat ?? 0m) + (bench ?? 0m) + (deadlift ?? 0m)
            : (decimal?)null;

        return placement with
        {
            PredictedSquatKg = squat,
            PredictedBenchKg = bench,
            PredictedDeadliftKg = deadlift,
            PredictedTotalKg = total,
            IsAutoSeeded = false
        };
    }

    public static AthletePlacementPick ApplyNudge(
        AthletePlacementPick placement,
        string field,
        decimal delta)
    {
        var current = field switch
        {
            TopNSheetField.Squat => placement.PredictedSquatKg,
            TopNSheetField.Bench => placement.PredictedBenchKg,
            TopNSheetField.Deadlift => placement.PredictedDeadliftKg,
            _ => placement.PredictedTotalKg
        } ?? 0m;

        var next = Math.Clamp(current + delta, 0m, 1500m);
        if (next <= 0m)
        {
            next = Math.Max(0m, delta > 0 ? delta : 0m);
        }

        return ApplyEdit(placement, field, next == 0m ? null : next);
    }

    public static bool TryReadOptionalPositiveDecimal(
        string value,
        CultureInfo culture,
        out decimal? parsed)
    {
        parsed = null;

        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        if ((!decimal.TryParse(value, NumberStyles.Number, culture, out var number) &&
             !decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out number)) ||
            number <= 0 ||
            number > 1500m)
        {
            return false;
        }

        parsed = number;
        return true;
    }

    public static bool TryReadNudgeDelta(string value, out decimal delta)
    {
        return decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out delta);
    }

    public static bool HasLiftValues(AthletePlacementPick? placement) =>
        placement?.PredictedSquatKg is not null ||
        placement?.PredictedBenchKg is not null ||
        placement?.PredictedDeadliftKg is not null;
}
