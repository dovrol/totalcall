using TotalCall.Core.Domain.Athletes;
using TotalCall.Core.Domain.Predictions;

namespace TotalCall.Client.Components.Predictions.TopN;

public static class TopNSlotViewBuilder
{
    public static IReadOnlyList<TopNSlotView> Build(
        IReadOnlyList<AthletePlacementPick> placements,
        IReadOnlyList<Athlete> athletes,
        IReadOnlyDictionary<int, string> modeByPosition,
        string defaultMode,
        TopNContextSelectionState contextSelection,
        IReadOnlyDictionary<string, AthleteHistoryEntry?> historyByAthleteId,
        IReadOnlySet<string> loadingAthleteIds)
    {
        var views = new List<TopNSlotView>(placements.Count);

        foreach (var placement in placements)
        {
            var athlete = athletes.First(item =>
                string.Equals(item.Id, placement.AthleteId, StringComparison.OrdinalIgnoreCase));

            historyByAthleteId.TryGetValue(athlete.Id, out var history);

            views.Add(new TopNSlotView
            {
                Position = placement.Position,
                Athlete = athlete,
                Placement = placement,
                Mode = TopNPlacementEditor.ResolveEntryMode(modeByPosition, placement.Position, placement, defaultMode),
                IsContextActive = contextSelection.IsSelected(athlete.Id),
                IsScored = placement.IsScored,
                IsAutoSeeded = placement.IsAutoSeeded,
                IsWithdrawn = athlete.IsWithdrawn,
                CanMoveUp = placement.Position > 1,
                CanMoveDown = placement.Position < placements.Count,
                CanUseNominated = athlete.SeedTotalKg is not null,
                CanUseLast = history?.LastResult?.TotalKg is not null,
                CanUseBest = history?.Bests?.TotalKg is not null,
                IsHistoryLoading = loadingAthleteIds.Contains(athlete.Id),
                NominatedTotalKg = athlete.SeedTotalKg,
                LastTotalKg = history?.LastResult?.TotalKg,
                BestTotalKg = history?.Bests?.TotalKg
            });
        }

        return views;
    }
}
