using TotalCall.Client.Domain.Predictions;

namespace TotalCall.Client.Scoring;

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

    public abstract PredictionQuestionType QuestionType { get; }

    public QuestionScoreResult Score(QuestionScoringContext context)
    {
        var requiredCount = GetRequiredCount(context.Question);
        var predictedPlacements = context.Answer.Value.AthletePlacements
            .Where(placement => placement.IsScored && !string.IsNullOrWhiteSpace(placement.AthleteId))
            .OrderBy(placement => placement.Position)
            .Take(requiredCount)
            .ToArray();

        var maxPoints = requiredCount * ExactPositionPoints;
        if (predictedPlacements.Length < requiredCount)
        {
            return CreateResult(context, 0m, maxPoints, "Incomplete prediction.");
        }

        var officialPlacements = context.ResultGroup.Placements
            .Where(placement => placement.Position >= 1 && !string.IsNullOrWhiteSpace(placement.AthleteId))
            .OrderBy(placement => placement.Position)
            .Take(requiredCount)
            .ToArray();

        var officialByPosition = officialPlacements.ToDictionary(placement => placement.Position);
        var officialAthleteIds = officialPlacements
            .Select(placement => placement.AthleteId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var points = 0m;
        foreach (var predicted in predictedPlacements)
        {
            if (officialByPosition.TryGetValue(predicted.Position, out var exactPlacement) &&
                string.Equals(exactPlacement.AthleteId, predicted.AthleteId, StringComparison.OrdinalIgnoreCase))
            {
                points += ExactPositionPoints;
                continue;
            }

            if (officialAthleteIds.Contains(predicted.AthleteId))
            {
                points += CorrectAthleteWrongPositionPoints;
            }
        }

        return CreateResult(context, points, maxPoints);
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
        decimal maxPoints,
        string? explanation = null)
    {
        return new QuestionScoreResult(
            context.Group.Id,
            context.Question.Id,
            points,
            maxPoints,
            context.Question.CategoryId,
            explanation);
    }
}
