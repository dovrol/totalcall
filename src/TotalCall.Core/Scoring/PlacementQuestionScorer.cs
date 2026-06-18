using TotalCall.Core.Domain.Athletes;
using TotalCall.Core.Domain.Predictions;

namespace TotalCall.Core.Scoring;

public sealed class AthleteRankingQuestionScorer : PlacementQuestionScorer
{
    public override PredictionQuestionType QuestionType => PredictionQuestionType.AthleteRanking;
}

public sealed class CategoryPodiumQuestionScorer : PlacementQuestionScorer
{
    public override PredictionQuestionType QuestionType => PredictionQuestionType.CategoryPodium;
}

public abstract class PlacementQuestionScorer : IQuestionScorer
{
    private const decimal ExactPositionPoints = 3m;
    private const decimal CorrectAthleteWrongPositionPoints = 1m;
    private const decimal SetBonusPoints = 1m;
    private const decimal PerfectOrderBonusPoints = 2m;

    public abstract PredictionQuestionType QuestionType { get; }

    public QuestionScoreResult Score(QuestionScoringContext context)
    {
        var requiredCount = GetRequiredCount(context.Question);
        var placementMax = requiredCount * ExactPositionPoints;
        var maxPoints = placementMax + SetBonusPoints + PerfectOrderBonusPoints;

        var predictedPlacements = context.Answer.Value.AthletePlacements
            .Where(placement => placement.IsScored && !string.IsNullOrWhiteSpace(placement.AthleteId))
            .OrderBy(placement => placement.Position)
            .Take(requiredCount)
            .ToArray();

        var officialPlacements = context.ResultGroup.Placements
            .Where(placement => placement.Position >= 1 && !string.IsNullOrWhiteSpace(placement.AthleteId))
            .OrderBy(placement => placement.Position)
            .Take(requiredCount)
            .ToArray();

        // The full category ranking (not just the scored Top N) so the board can show
        // an official place badge for every athlete, including the comparison zone.
        var official = context.ResultGroup.Placements
            .Where(placement => placement.Position >= 1 && !string.IsNullOrWhiteSpace(placement.AthleteId))
            .OrderBy(placement => placement.Position)
            .Select(placement => new OfficialPlacementRef(
                placement.Position,
                placement.AthleteId,
                placement.TotalKg,
                placement.SquatKg,
                placement.BenchKg,
                placement.DeadliftKg))
            .ToArray();

        if (predictedPlacements.Length < requiredCount)
        {
            return CreateResult(
                context,
                points: 0m,
                placementPoints: 0m,
                placementMax: placementMax,
                setBonus: 0m,
                orderBonus: 0m,
                maxPoints: maxPoints,
                slots: [],
                official: official,
                explanation: "Incomplete prediction.");
        }

        var officialByPosition = officialPlacements.ToDictionary(placement => placement.Position);
        var officialAthleteIds = officialPlacements
            .Select(placement => placement.AthleteId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var withdrawnAthleteIds = context.Competition.Athletes
            .Where(athlete => athlete.IsWithdrawn)
            .Select(athlete => athlete.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var slots = new List<SlotScoreResult>(predictedPlacements.Length);
        var placementPoints = 0m;

        foreach (var predicted in predictedPlacements)
        {
            string verdict;
            decimal points;

            if (withdrawnAthleteIds.Contains(predicted.AthleteId))
            {
                verdict = SlotVerdict.Withdrawn;
                points = 0m;
            }
            else if (officialByPosition.TryGetValue(predicted.Position, out var exactPlacement) &&
                     string.Equals(exactPlacement.AthleteId, predicted.AthleteId, StringComparison.OrdinalIgnoreCase))
            {
                verdict = SlotVerdict.Exact;
                points = ExactPositionPoints;
            }
            else if (officialAthleteIds.Contains(predicted.AthleteId))
            {
                verdict = SlotVerdict.Wrong;
                points = CorrectAthleteWrongPositionPoints;
            }
            else
            {
                verdict = SlotVerdict.Miss;
                points = 0m;
            }

            placementPoints += points;
            slots.Add(new SlotScoreResult(predicted.Position, predicted.AthleteId, verdict, points));
        }

        // Set bonus: every official Top-N athlete is present among the (non-withdrawn) picks.
        var nonWithdrawnPickIds = predictedPlacements
            .Where(placement => !withdrawnAthleteIds.Contains(placement.AthleteId))
            .Select(placement => placement.AthleteId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var setBonus = officialPlacements.Length > 0 &&
                       officialPlacements.All(placement => nonWithdrawnPickIds.Contains(placement.AthleteId))
            ? SetBonusPoints
            : 0m;

        // Perfect order: every scored slot landed exact.
        var orderBonus = slots.Count > 0 && slots.All(slot => slot.Verdict == SlotVerdict.Exact)
            ? PerfectOrderBonusPoints
            : 0m;

        var total = placementPoints + setBonus + orderBonus;

        return CreateResult(
            context,
            points: total,
            placementPoints: placementPoints,
            placementMax: placementMax,
            setBonus: setBonus,
            orderBonus: orderBonus,
            maxPoints: maxPoints,
            slots: slots,
            official: official,
            explanation: null);
    }

    private static int GetRequiredCount(PredictionQuestion question)
    {
        if (question.Constraints.ExactSelections is { } exactSelections)
        {
            return exactSelections;
        }

        if (question.Constraints.MaxSelections is { } maxSelections)
        {
            return maxSelections;
        }

        return 3;
    }

    private static QuestionScoreResult CreateResult(
        QuestionScoringContext context,
        decimal points,
        decimal placementPoints,
        decimal placementMax,
        decimal setBonus,
        decimal orderBonus,
        decimal maxPoints,
        IReadOnlyList<SlotScoreResult> slots,
        IReadOnlyList<OfficialPlacementRef> official,
        string? explanation)
    {
        return new QuestionScoreResult(
            context.Group.Id,
            context.Question.Id,
            points,
            maxPoints,
            context.Question.CategoryId,
            explanation,
            slots,
            official,
            placementPoints,
            placementMax,
            setBonus,
            orderBonus);
    }
}
