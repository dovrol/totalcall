using TotalCall.Core.Domain.Athletes;
using TotalCall.Core.Domain.Predictions;

namespace TotalCall.Client.Components.Predictions.TopN;

public sealed record TopNContextSelectionState(string? SelectedAthleteId)
{
    public static TopNContextSelectionState Empty { get; } = new(SelectedAthleteId: null);

    public bool IsOpen => !string.IsNullOrWhiteSpace(SelectedAthleteId);

    public TopNContextSelectionState Open(string? athleteId)
    {
        return string.IsNullOrWhiteSpace(athleteId)
            ? Empty
            : new TopNContextSelectionState(athleteId);
    }

    public TopNContextSelectionState Close()
    {
        return Empty;
    }

    public TopNContextSelectionState EnsureSelectable(Func<string, bool> isAthleteSelectable)
    {
        return IsOpen && isAthleteSelectable(SelectedAthleteId!)
            ? this
            : Empty;
    }

    public bool IsSelected(string? athleteId)
    {
        return !string.IsNullOrWhiteSpace(athleteId) &&
               string.Equals(athleteId, SelectedAthleteId, StringComparison.OrdinalIgnoreCase);
    }

    public int? GetSelectedRank(IReadOnlyList<AthletePlacementPick> placements)
    {
        return placements
            .FirstOrDefault(placement => IsSelected(placement.AthleteId))
            ?.Position;
    }

    public decimal? GetSelectedPredictedTotal(IReadOnlyList<AthletePlacementPick> placements)
    {
        return placements
            .FirstOrDefault(placement => IsSelected(placement.AthleteId))
            ?.PredictedTotalKg;
    }

    public IReadOnlyDictionary<int, Athlete> BuildSelectedAthletesByPosition(
        IReadOnlyList<AthletePlacementPick> placements,
        IReadOnlyList<Athlete> athletes)
    {
        return placements
            .Select(placement => new
            {
                placement.Position,
                Athlete = athletes.FirstOrDefault(athlete =>
                    string.Equals(athlete.Id, placement.AthleteId, StringComparison.OrdinalIgnoreCase))
            })
            .Where(item => item.Athlete is not null)
            .ToDictionary(item => item.Position, item => item.Athlete!, EqualityComparer<int>.Default);
    }
}
