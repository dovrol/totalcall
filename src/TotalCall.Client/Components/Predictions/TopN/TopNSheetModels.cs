using TotalCall.Client.Domain.Athletes;
using TotalCall.Client.Domain.Predictions;

namespace TotalCall.Client.Components.Predictions.TopN;

/// <summary>Default entry mode for prediction cells in the Top N sheet.</summary>
public static class TopNEntryMode
{
    public const string Lifts = "lifts";
    public const string Total = "total";

    public static string Normalize(string? value) =>
        string.Equals(value, Lifts, StringComparison.OrdinalIgnoreCase) ? Lifts : Total;
}

/// <summary>Quick-fill source applied to a pending sheet row.</summary>
public static class TopNQuickFillKind
{
    public const string Nominated = "nominated";
    public const string Last = "last";
    public const string Best = "best";
}

/// <summary>Editable field targeted by an inline sheet edit.</summary>
public static class TopNSheetField
{
    public const string Squat = "squat";
    public const string Bench = "bench";
    public const string Deadlift = "deadlift";
    public const string Total = "total";
}

/// <summary>A single Top N ranking slot rendered as a sheet row.</summary>
public sealed record TopNSlotView
{
    public required int Position { get; init; }

    public Athlete? Athlete { get; init; }

    public AthletePlacementPick? Placement { get; init; }

    public string Mode { get; init; } = TopNEntryMode.Total;

    public bool IsContextActive { get; init; }

    public bool IsRemovePending { get; init; }

    public IReadOnlyList<decimal> Trend { get; init; } = [];

    public decimal? TrendDeltaKg { get; init; }

    public bool CanUseNominated { get; init; }

    public bool CanUseLast { get; init; }

    public bool CanUseBest { get; init; }

    public decimal? NominatedTotalKg { get; init; }

    public decimal? LastTotalKg { get; init; }

    public decimal? BestTotalKg { get; init; }
}

/// <summary>An inline edit emitted by a sheet cell.</summary>
public sealed record TopNSheetEdit(int Position, string Field, string Value);

/// <summary>A quick-fill request emitted by a pending sheet row.</summary>
public sealed record TopNQuickFill(int Position, string Kind);
