using TotalCall.Core.Domain.Athletes;
using TotalCall.Core.Domain.Predictions;

namespace TotalCall.Client.Components.Predictions.TopN;

public static class TopNQuickFillBuilder
{
    public static AthletePlacementPick? BuildFromSource(
        AthletePlacementPick placement,
        Athlete athlete,
        AthleteHistoryEntry? history,
        string kind)
    {
        return kind switch
        {
            TopNQuickFillKind.Nominated when athlete.SeedTotalKg is not null => placement with
            {
                PredictedSquatKg = null,
                PredictedBenchKg = null,
                PredictedDeadliftKg = null,
                PredictedTotalKg = athlete.SeedTotalKg,
                IsAutoSeeded = false
            },
            TopNQuickFillKind.Last when history?.LastResult?.TotalKg is not null => FromResult(
                placement,
                history.LastResult.SquatKg,
                history.LastResult.BenchKg,
                history.LastResult.DeadliftKg,
                history.LastResult.TotalKg),
            TopNQuickFillKind.Best when history?.Bests?.TotalKg is not null => FromResult(
                placement,
                history.Bests.SquatKg,
                history.Bests.BenchKg,
                history.Bests.DeadliftKg,
                history.Bests.TotalKg),
            _ => null
        };
    }

    private static AthletePlacementPick FromResult(
        AthletePlacementPick placement,
        decimal? squat,
        decimal? bench,
        decimal? deadlift,
        decimal? total)
    {
        if (squat is not null || bench is not null || deadlift is not null)
        {
            return placement with
            {
                PredictedSquatKg = squat,
                PredictedBenchKg = bench,
                PredictedDeadliftKg = deadlift,
                PredictedTotalKg = total ?? (squat ?? 0m) + (bench ?? 0m) + (deadlift ?? 0m),
                IsAutoSeeded = false
            };
        }

        return placement with
        {
            PredictedSquatKg = null,
            PredictedBenchKg = null,
            PredictedDeadliftKg = null,
            PredictedTotalKg = total,
            IsAutoSeeded = false
        };
    }
}
