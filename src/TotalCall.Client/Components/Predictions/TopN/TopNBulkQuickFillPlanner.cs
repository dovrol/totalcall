using TotalCall.Core.Domain.Athletes;
using TotalCall.Core.Domain.Predictions;

namespace TotalCall.Client.Components.Predictions.TopN;

public sealed record TopNBulkQuickFillResult(
    IReadOnlyList<AthletePlacementPick> Placements,
    IReadOnlyDictionary<int, string> ModeByPosition,
    bool Changed);

public static class TopNBulkQuickFillPlanner
{
    public static IReadOnlyList<int> GetDefaultPositions(IReadOnlyList<AthletePlacementPick> placements)
    {
        return placements
            .Where(placement => placement.IsAutoSeeded)
            .Select(placement => placement.Position)
            .ToArray();
    }

    public static TopNBulkQuickFillResult FillDefaultPositions(
        IReadOnlyList<AthletePlacementPick> placements,
        IReadOnlyList<int> defaultPositions,
        IReadOnlyList<Athlete> athletes,
        IReadOnlyDictionary<string, AthleteHistoryEntry?> historyByAthleteId,
        string kind)
    {
        var updated = placements.ToList();
        var modes = new Dictionary<int, string>();
        var changed = false;

        foreach (var position in defaultPositions)
        {
            var index = updated.FindIndex(placement => placement.Position == position);
            if (index < 0)
            {
                continue;
            }

            var placement = updated[index];
            var athlete = athletes.First(item =>
                string.Equals(item.Id, placement.AthleteId, StringComparison.OrdinalIgnoreCase));

            historyByAthleteId.TryGetValue(athlete.Id, out var history);

            var filled = TopNQuickFillBuilder.BuildFromSource(placement, athlete, history, kind);
            if (filled is null)
            {
                continue;
            }

            updated[index] = filled;
            modes[position] = TopNPlacementEditor.HasLiftValues(filled)
                ? TopNEntryMode.Lifts
                : TopNEntryMode.Total;
            changed = true;
        }

        return new TopNBulkQuickFillResult(updated, modes, changed);
    }
}
