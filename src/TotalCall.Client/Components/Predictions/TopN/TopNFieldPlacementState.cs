using TotalCall.Core.Domain.Athletes;
using TotalCall.Core.Domain.Competitions;
using TotalCall.Core.Domain.Predictions;

namespace TotalCall.Client.Components.Predictions.TopN;

public sealed record TopNFieldPlacementState(
    IReadOnlyList<Athlete> QuestionAthletes,
    IReadOnlyList<Athlete> EligibleAthletes,
    IReadOnlyList<AthletePlacementPick> FieldPlacements,
    int ScoredPositionsCount)
{
    private readonly HashSet<string> questionAthleteIds = QuestionAthletes
        .Select(athlete => athlete.Id)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    public static TopNFieldPlacementState Resolve(
        Competition competition,
        PredictionQuestion? activeQuestion,
        IReadOnlyList<AthletePlacementPick> storedPlacements,
        int scoredPositionsCount)
    {
        if (activeQuestion is null)
        {
            return new TopNFieldPlacementState([], [], [], scoredPositionsCount);
        }

        var questionAthletes = ResolveQuestionAthletes(competition, activeQuestion);
        var eligibleAthletes = questionAthletes
            .Where(athlete => !athlete.IsWithdrawn)
            .ToArray();
        var fieldPlacements = BuildFieldPlacements(
            questionAthletes,
            eligibleAthletes,
            storedPlacements,
            scoredPositionsCount);

        return new TopNFieldPlacementState(
            questionAthletes,
            eligibleAthletes,
            fieldPlacements,
            scoredPositionsCount);
    }

    public IReadOnlyList<AthletePlacementPick> BuildDefaultFieldPlacements()
    {
        return EligibleAthletes
            .Select((athlete, index) => new AthletePlacementPick
            {
                Position = index + 1,
                AthleteId = athlete.Id,
                PredictedTotalKg = athlete.SeedTotalKg,
                IsScored = index < ScoredPositionsCount,
                IsAutoSeeded = true
            })
            .ToArray();
    }

    public IReadOnlyList<AthletePlacementPick> NormalizeForAnswer(IEnumerable<AthletePlacementPick> placements)
    {
        return placements
            .Where(placement => !string.IsNullOrWhiteSpace(placement.AthleteId) &&
                                questionAthleteIds.Contains(placement.AthleteId))
            .GroupBy(placement => placement.AthleteId, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(placement => placement.Position)
            .Select((placement, index) => placement with
            {
                Position = index + 1,
                IsScored = index < ScoredPositionsCount
            })
            .ToArray();
    }

    public static bool HasSameField(
        IReadOnlyList<AthletePlacementPick> stored,
        IReadOnlyList<AthletePlacementPick> normalized)
    {
        if (stored.Count != normalized.Count)
        {
            return false;
        }

        for (var index = 0; index < stored.Count; index++)
        {
            if (stored[index].Position != normalized[index].Position ||
                stored[index].IsScored != normalized[index].IsScored ||
                stored[index].IsAutoSeeded != normalized[index].IsAutoSeeded ||
                !string.Equals(stored[index].AthleteId, normalized[index].AthleteId, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private static IReadOnlyList<Athlete> ResolveQuestionAthletes(
        Competition competition,
        PredictionQuestion activeQuestion)
    {
        var athletes = activeQuestion.AthleteIds.Count == 0
            ? competition.Athletes
            : competition.Athletes
                .Where(athlete => activeQuestion.AthleteIds.Contains(athlete.Id))
                .ToArray();

        return athletes
            .OrderByDescending(athlete => athlete.SeedTotalKg ?? 0m)
            .ThenBy(athlete => athlete.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyList<AthletePlacementPick> BuildFieldPlacements(
        IReadOnlyList<Athlete> questionAthletes,
        IReadOnlyList<Athlete> eligibleAthletes,
        IReadOnlyList<AthletePlacementPick> storedPlacements,
        int scoredPositionsCount)
    {
        var questionAthletesById = questionAthletes.ToDictionary(athlete => athlete.Id, StringComparer.OrdinalIgnoreCase);
        var seenAthleteIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var field = new List<AthletePlacementPick>(questionAthletesById.Count);

        foreach (var placement in storedPlacements.OrderBy(item => item.Position))
        {
            if (questionAthletesById.ContainsKey(placement.AthleteId) && seenAthleteIds.Add(placement.AthleteId))
            {
                field.Add(placement);
            }
        }

        foreach (var athlete in eligibleAthletes)
        {
            if (!seenAthleteIds.Add(athlete.Id))
            {
                continue;
            }

            field.Add(new AthletePlacementPick
            {
                Position = field.Count + 1,
                AthleteId = athlete.Id,
                PredictedTotalKg = athlete.SeedTotalKg,
                IsAutoSeeded = true
            });
        }

        return field
            .Select((placement, index) => placement with
            {
                Position = index + 1,
                IsScored = index < scoredPositionsCount
            })
            .ToArray();
    }
}
