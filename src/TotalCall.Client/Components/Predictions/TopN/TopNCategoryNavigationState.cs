using TotalCall.Core.Domain.Predictions;

namespace TotalCall.Client.Components.Predictions.TopN;

public sealed record TopNCategoryNavigationState(
    IReadOnlyList<PredictionQuestion> OrderedQuestions,
    PredictionQuestion? ActiveQuestion,
    IReadOnlyDictionary<string, PredictionQuestionCompletionResult> StatusByQuestionId,
    PredictionQuestionCompletionResult? ActiveQuestionStatus,
    int ActiveQuestionIndex,
    int CompletedCategoriesCount,
    PredictionCompletionStatus ActiveStatusKind)
{
    public static TopNCategoryNavigationState Resolve(
        PredictionGroup activeGroup,
        string? activeQuestionId,
        PredictionModuleValidationResult activeModuleValidation)
    {
        var orderedQuestions = activeGroup.Questions
            .OrderBy(question => question.Order)
            .ToArray();
        var activeQuestion = orderedQuestions
            .FirstOrDefault(question => string.Equals(question.Id, activeQuestionId, StringComparison.OrdinalIgnoreCase)) ??
            orderedQuestions.FirstOrDefault();
        var statusByQuestionId = activeModuleValidation.Questions
            .ToDictionary(result => result.QuestionId, StringComparer.OrdinalIgnoreCase);
        var activeQuestionStatus = activeQuestion is not null &&
                                   statusByQuestionId.TryGetValue(activeQuestion.Id, out var status)
            ? status
            : null;
        var activeQuestionIndex = ResolveActiveQuestionIndex(orderedQuestions, activeQuestion);
        var completedCategoriesCount = orderedQuestions
            .Count(question => statusByQuestionId.TryGetValue(question.Id, out var status) &&
                               status.Status == PredictionCompletionStatus.Complete);

        return new TopNCategoryNavigationState(
            orderedQuestions,
            activeQuestion,
            statusByQuestionId,
            activeQuestionStatus,
            activeQuestionIndex,
            completedCategoriesCount,
            activeQuestionStatus?.Status ?? PredictionCompletionStatus.NotStarted);
    }

    private static int ResolveActiveQuestionIndex(
        IReadOnlyList<PredictionQuestion> orderedQuestions,
        PredictionQuestion? activeQuestion)
    {
        if (activeQuestion is null)
        {
            return 0;
        }

        for (var index = 0; index < orderedQuestions.Count; index++)
        {
            if (orderedQuestions[index].Id == activeQuestion.Id)
            {
                return index;
            }
        }

        return 0;
    }
}
