using TotalCall.Core.Domain.Predictions;

namespace TotalCall.Client.Components.Predictions.TopN;

public sealed record TopNReorderPlan(
    IReadOnlyList<AthletePlacementPick> BeforeOrder,
    IReadOnlyList<AthletePlacementPick> AfterOrder);

public static class TopNReorderPlanner
{
    public static TopNReorderPlan? MoveToPosition(
        IReadOnlyList<AthletePlacementPick> placements,
        int from,
        int to,
        int scoredPositionsCount)
    {
        if (placements.Count == 0)
        {
            return null;
        }

        to = Math.Clamp(to, 1, placements.Count);
        if (from == to)
        {
            return null;
        }

        var ordered = placements.OrderBy(placement => placement.Position).ToList();
        var source = ordered.FirstOrDefault(placement => placement.Position == from);
        if (source is null)
        {
            return null;
        }

        ordered.Remove(source);

        var insertIndex = Math.Clamp(to - 1, 0, ordered.Count);
        ordered.Insert(insertIndex, source);

        var renumbered = ordered
            .Select((placement, index) => placement with
            {
                Position = index + 1,
                IsScored = index < scoredPositionsCount
            })
            .ToArray();

        return new TopNReorderPlan(ordered, renumbered);
    }

    public static IReadOnlyDictionary<int, string> RemapModesAfterReorder(
        IReadOnlyDictionary<int, string> modeByPosition,
        IReadOnlyList<AthletePlacementPick> beforeOrder,
        IReadOnlyList<AthletePlacementPick> afterOrder)
    {
        if (modeByPosition.Count == 0)
        {
            return new Dictionary<int, string>();
        }

        var modeByAthlete = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var placement in beforeOrder)
        {
            if (modeByPosition.TryGetValue(placement.Position, out var mode))
            {
                modeByAthlete[placement.AthleteId] = mode;
            }
        }

        var remapped = new Dictionary<int, string>();
        foreach (var placement in afterOrder)
        {
            if (modeByAthlete.TryGetValue(placement.AthleteId, out var mode))
            {
                remapped[placement.Position] = mode;
            }
        }

        return remapped;
    }
}
