using TotalCall.Core.Domain.Athletes;
using TotalCall.Core.Domain.Predictions;

namespace TotalCall.Client.Components.Predictions.TopN;

public sealed record TopNRowQuickFillTarget(
    AthletePlacementPick Placement,
    Athlete Athlete);

public sealed record TopNRowQuickFillResult(
    Athlete Athlete,
    string Mode,
    IReadOnlyList<AthletePlacementPick> Placements);

public static class TopNRowQuickFillPlanner
{
    public static TopNRowQuickFillTarget? ResolveTarget(
        IReadOnlyList<AthletePlacementPick> placements,
        IReadOnlyList<Athlete> athletes,
        int position)
    {
        var placement = placements.FirstOrDefault(item => item.Position == position);
        if (placement is null)
        {
            return null;
        }

        var athlete = athletes.FirstOrDefault(item =>
            string.Equals(item.Id, placement.AthleteId, StringComparison.OrdinalIgnoreCase));
        return athlete is null ? null : new TopNRowQuickFillTarget(placement, athlete);
    }

    public static TopNRowQuickFillResult? Fill(
        IReadOnlyList<AthletePlacementPick> placements,
        TopNRowQuickFillTarget target,
        AthleteHistoryEntry? history,
        string kind)
    {
        var updated = TopNQuickFillBuilder.BuildFromSource(target.Placement, target.Athlete, history, kind);
        if (updated is null)
        {
            return null;
        }

        var mode = TopNPlacementEditor.HasLiftValues(updated)
            ? TopNEntryMode.Lifts
            : TopNEntryMode.Total;

        var next = placements
            .Select(item => item.Position == target.Placement.Position ? updated : item)
            .ToArray();

        return new TopNRowQuickFillResult(target.Athlete, mode, next);
    }
}
