using TotalCall.Core.Domain.Predictions;

namespace TotalCall.Client.Components.Predictions.TopN;

public static class TopNSortPlanner
{
    public static IReadOnlyList<AthletePlacementPick>? SortByPredictedTotal(
        IReadOnlyList<AthletePlacementPick> placements,
        int scoredPositionsCount)
    {
        if (placements.Count == 0 || placements.All(placement => placement.PredictedTotalKg is null))
        {
            return null;
        }

        return placements
            .Select((placement, index) => new { placement, index })
            .OrderBy(item => item.placement.PredictedTotalKg is null ? 1 : 0)
            .ThenByDescending(item => item.placement.PredictedTotalKg ?? 0m)
            .ThenBy(item => item.index)
            .Select((item, index) => item.placement with
            {
                Position = index + 1,
                IsScored = index < scoredPositionsCount
            })
            .ToArray();
    }
}
