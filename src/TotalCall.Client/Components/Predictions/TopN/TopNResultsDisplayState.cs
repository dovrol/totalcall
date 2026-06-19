using TotalCall.Core.Domain.Predictions;
using TotalCall.Core.Domain.Predictions.Results;

namespace TotalCall.Client.Components.Predictions.TopN;

public sealed record TopNResultsDisplayState(
    bool HasResultsImported,
    CategoryScoreBreakdown? ActiveBreakdown,
    bool IsActiveCategoryScored,
    IReadOnlyDictionary<string, TopNSlotResult>? ActiveSheetResults,
    IReadOnlyList<PredictionQuestion> PlacementQuestions,
    int ResultsTotalCount,
    decimal MaxResultsPoints)
{
    public static TopNResultsDisplayState Resolve(
        PredictionGroup activeGroup,
        PredictionQuestion? activeQuestion,
        MyScoreSnapshot? score)
    {
        var placementQuestions = activeGroup.Questions
            .OrderBy(question => question.Order)
            .Where(question => question.Type is PredictionQuestionType.AthleteRanking or PredictionQuestionType.CategoryPodium)
            .ToArray();
        var activeBreakdown = activeQuestion is null
            ? null
            : score?.FindCategory(activeGroup.Id, activeQuestion.Id);

        return new TopNResultsDisplayState(
            score is not null,
            activeBreakdown,
            activeBreakdown is not null,
            BuildActiveSheetResults(activeBreakdown),
            placementQuestions,
            score?.TotalGroupsCount ?? placementQuestions.Length,
            placementQuestions.Sum(MaxPointsForQuestion));
    }

    private static IReadOnlyDictionary<string, TopNSlotResult>? BuildActiveSheetResults(
        CategoryScoreBreakdown? breakdown)
    {
        if (breakdown is null)
        {
            return null;
        }

        var officialByAthlete = breakdown.Official
            .GroupBy(placement => placement.AthleteId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var map = new Dictionary<string, TopNSlotResult>(StringComparer.OrdinalIgnoreCase);

        foreach (var placement in breakdown.Official)
        {
            map[placement.AthleteId] = ToTopNSlotResult(placement, null);
        }

        foreach (var slot in breakdown.Slots)
        {
            officialByAthlete.TryGetValue(slot.AthleteId, out var official);
            map[slot.AthleteId] = ToTopNSlotResult(official, slot);
        }

        return map;
    }

    private static decimal MaxPointsForQuestion(PredictionQuestion question)
    {
        var required = question.Constraints.ExactSelections ?? question.Constraints.MaxSelections ?? 3;
        return required * 3m + 3m;
    }

    private static TopNSlotResult ToTopNSlotResult(CategoryOfficialPlacement? official, CategorySlotResult? slot) =>
        new(
            official?.Position,
            slot?.Verdict,
            slot?.Points,
            official?.TotalKg,
            official?.SquatKg,
            official?.BenchKg,
            official?.DeadliftKg);
}
