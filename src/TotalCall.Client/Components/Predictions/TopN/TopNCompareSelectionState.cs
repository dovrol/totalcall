using TotalCall.Client.Application.Windows;
using TotalCall.Core.Domain.Athletes;
using TotalCall.Core.Domain.Predictions;

namespace TotalCall.Client.Components.Predictions.TopN;

public enum TopNCompareSelectionToggleResult
{
    Ignored,
    Started,
    Added,
    Removed,
    MaxReached
}

public sealed record TopNCompareSelectionState(
    bool IsActive,
    IReadOnlyCollection<string> SelectedAthleteIds)
{
    public const int MaxSelections = 3;

    public static TopNCompareSelectionState Empty { get; } = new(false, []);

    public TopNCompareSelectionState Start(string? preselectedAthleteId, Func<string, bool> isAthleteSelectable)
    {
        if (!string.IsNullOrWhiteSpace(preselectedAthleteId) && isAthleteSelectable(preselectedAthleteId))
        {
            return new TopNCompareSelectionState(true, [preselectedAthleteId]);
        }

        return new TopNCompareSelectionState(true, []);
    }

    public TopNCompareSelectionState Cancel()
    {
        return Empty;
    }

    public (TopNCompareSelectionState State, TopNCompareSelectionToggleResult Result) Toggle(
        string athleteId,
        Func<string, bool> isAthleteSelectable)
    {
        if (string.IsNullOrWhiteSpace(athleteId) || !isAthleteSelectable(athleteId))
        {
            return (this, TopNCompareSelectionToggleResult.Ignored);
        }

        if (!IsActive)
        {
            return (Start(athleteId, isAthleteSelectable), TopNCompareSelectionToggleResult.Started);
        }

        var selected = SelectedAthleteIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!selected.Add(athleteId))
        {
            selected.Remove(athleteId);
            return (new TopNCompareSelectionState(true, selected.ToArray()), TopNCompareSelectionToggleResult.Removed);
        }

        if (selected.Count > MaxSelections)
        {
            return (this, TopNCompareSelectionToggleResult.MaxReached);
        }

        return (new TopNCompareSelectionState(true, selected.ToArray()), TopNCompareSelectionToggleResult.Added);
    }

    public IReadOnlyList<AthleteComparisonWindowSlot> BuildSelectedSlots(
        IReadOnlyList<AthletePlacementPick> fieldPlacements,
        IReadOnlyList<Athlete> athletes)
    {
        var selected = SelectedAthleteIds.ToHashSet(StringComparer.OrdinalIgnoreCase);

        return fieldPlacements
            .Where(placement => selected.Contains(placement.AthleteId))
            .OrderBy(placement => placement.Position)
            .Select(placement => ToComparisonSlot(placement, athletes))
            .OfType<AthleteComparisonWindowSlot>()
            .ToArray();
    }

    private static AthleteComparisonWindowSlot? ToComparisonSlot(
        AthletePlacementPick placement,
        IReadOnlyList<Athlete> athletes)
    {
        var athlete = athletes.FirstOrDefault(item =>
            string.Equals(item.Id, placement.AthleteId, StringComparison.OrdinalIgnoreCase));

        if (athlete is null)
        {
            return null;
        }

        return new AthleteComparisonWindowSlot
        {
            Position = placement.Position,
            AthleteId = athlete.Id,
            AthleteName = athlete.DisplayName,
            CountryCode = athlete.CountryCode,
            CountryName = athlete.CountryName,
            NominatedTotalKg = athlete.SeedTotalKg,
            PredictedTotalKg = placement.PredictedTotalKg,
            PredictedSquatKg = placement.PredictedSquatKg,
            PredictedBenchKg = placement.PredictedBenchKg,
            PredictedDeadliftKg = placement.PredictedDeadliftKg,
            IsAutoSeeded = placement.IsAutoSeeded
        };
    }
}
