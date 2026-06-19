using TotalCall.Core.Domain.Athletes;
using TotalCall.Core.Domain.Predictions;

namespace TotalCall.Client.Components.Predictions.TopN;

public sealed record TopNAthleteResetResult(
    Athlete Athlete,
    IReadOnlyList<AthletePlacementPick> Placements);

public static class TopNAthleteResetPlanner
{
    public static TopNAthleteResetResult? ResetAthlete(
        IReadOnlyList<AthletePlacementPick> placements,
        IReadOnlyList<Athlete> athletes,
        int position)
    {
        var placement = placements.FirstOrDefault(item => item.Position == position);
        if (placement is null)
        {
            return null;
        }

        var athlete = athletes.First(item =>
            string.Equals(item.Id, placement.AthleteId, StringComparison.OrdinalIgnoreCase));

        var updated = placements
            .Select(item => item.Position == position
                ? item with
                {
                    PredictedSquatKg = null,
                    PredictedBenchKg = null,
                    PredictedDeadliftKg = null,
                    PredictedTotalKg = athlete.SeedTotalKg,
                    IsAutoSeeded = true
                }
                : item)
            .ToArray();

        return new TopNAthleteResetResult(athlete, updated);
    }
}
