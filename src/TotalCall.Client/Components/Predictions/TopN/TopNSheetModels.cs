using TotalCall.Core.Domain.Athletes;
using TotalCall.Core.Domain.Predictions;

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

/// <summary>A single athlete in the ranked field rendered as a sheet row.</summary>
public sealed record TopNSlotView
{
    public required int Position { get; init; }

    public Athlete? Athlete { get; init; }

    public AthletePlacementPick? Placement { get; init; }

    public string Mode { get; init; } = TopNEntryMode.Total;

    public bool IsContextActive { get; init; }

    public bool IsScored { get; init; }

    public bool IsAutoSeeded { get; init; }

    public bool IsWithdrawn { get; init; }

    public bool CanMoveUp { get; init; }

    public bool CanMoveDown { get; init; }

    public bool CanUseNominated { get; init; }

    public bool CanUseLast { get; init; }

    public bool CanUseBest { get; init; }

    public bool IsHistoryLoading { get; init; }

    public decimal? NominatedTotalKg { get; init; }

    public decimal? LastTotalKg { get; init; }

    public decimal? BestTotalKg { get; init; }
}

/// <summary>An inline edit emitted by a sheet cell.</summary>
public sealed record TopNSheetEdit(int Position, string Field, string Value);

/// <summary>A quick-fill request emitted by a pending sheet row.</summary>
public sealed record TopNQuickFill(int Position, string Kind);

/// <summary>A reorder request: move the slot at <see cref="Position"/> by <see cref="Delta"/> places.</summary>
public sealed record TopNMove(int Position, int Delta);

/// <summary>
/// Backend scoring overlay for one athlete row, shown after results import.
/// <see cref="OfficialPlace"/> is the athlete's official finishing place (null
/// when they have no official result). <see cref="Verdict"/> / <see cref="Points"/>
/// are only set for the user's scored Top N picks; comparison-zone athletes show
/// just their place.
/// </summary>
public sealed record TopNSlotResult(
    int? OfficialPlace,
    string? Verdict,
    decimal? Points,
    decimal? OfficialTotalKg = null,
    decimal? OfficialSquatKg = null,
    decimal? OfficialBenchKg = null,
    decimal? OfficialDeadliftKg = null);
