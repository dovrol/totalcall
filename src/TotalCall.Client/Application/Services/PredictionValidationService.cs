using TotalCall.Client.Domain.Competitions;
using TotalCall.Client.Domain.Predictions;

namespace TotalCall.Client.Application.Services;

public sealed class PredictionValidationService : IPredictionValidationService
{
    public PredictionValidationResult Validate(Competition competition, PredictionSet predictionSet)
    {
        var errors = new List<PredictionValidationError>();

        foreach (var group in competition.PredictionGroups)
        {
            var moduleResult = ValidateModule(competition, group, predictionSet);
            errors.AddRange(moduleResult.ValidationErrors);
        }

        return new PredictionValidationResult(errors);
    }

    public PredictionModuleValidationResult ValidateModule(
        Competition competition,
        PredictionGroup group,
        PredictionSet predictionSet)
    {
        _ = competition;

        var orderedQuestions = group.Questions
            .OrderBy(question => question.Order)
            .ToArray();

        var questionResults = new List<PredictionQuestionCompletionResult>(orderedQuestions.Length);
        var validationErrors = new List<PredictionValidationError>();

        foreach (var question in orderedQuestions)
        {
            var answer = predictionSet.Answers.FirstOrDefault(candidate => candidate.QuestionId == question.Id);
            var questionErrors = ValidateQuestion(group, question, answer);
            validationErrors.AddRange(questionErrors);

            var selectedCount = GetSelectedCount(question, answer);
            var requiredCount = GetRequiredCount(question);
            var status = ResolveQuestionStatus(question, answer, questionErrors, selectedCount);

            questionResults.Add(new PredictionQuestionCompletionResult(
                question.Id,
                status,
                selectedCount,
                requiredCount));
        }

        var totalItems = questionResults.Count;
        var completedItems = questionResults.Count(result => result.Status == PredictionCompletionStatus.Complete);
        var inProgressItems = questionResults.Count(result => result.Status == PredictionCompletionStatus.InProgress);
        var notStartedItems = questionResults.Count(result => result.Status == PredictionCompletionStatus.NotStarted);

        var moduleStatus = completedItems == totalItems && totalItems > 0
            ? PredictionCompletionStatus.Complete
            : completedItems > 0 || inProgressItems > 0
                ? PredictionCompletionStatus.InProgress
                : PredictionCompletionStatus.NotStarted;

        return new PredictionModuleValidationResult(
            group.Id,
            moduleStatus,
            totalItems,
            completedItems,
            inProgressItems,
            notStartedItems,
            questionResults,
            validationErrors);
    }

    private static IReadOnlyList<PredictionValidationError> ValidateQuestion(
        PredictionGroup group,
        PredictionQuestion question,
        PredictionAnswer? answer)
    {
        if (answer is null)
        {
            return [];
        }

        var errors = new List<PredictionValidationError>();

        ValidateSelectionCount(group, question, answer, errors);
        ValidateNumericRange(group, question, answer, errors);
        ValidateDuplicateAthletes(group, question, answer, errors);

        return errors;
    }

    private static PredictionCompletionStatus ResolveQuestionStatus(
        PredictionQuestion question,
        PredictionAnswer? answer,
        IReadOnlyList<PredictionValidationError> questionErrors,
        int selectedCount)
    {
        if (answer is null)
        {
            return PredictionCompletionStatus.NotStarted;
        }

        var status = question.Type switch
        {
            PredictionQuestionType.YesNo => answer.Value.BooleanValue.HasValue
                ? PredictionCompletionStatus.Complete
                : PredictionCompletionStatus.NotStarted,
            PredictionQuestionType.NumericQuestion => answer.Value.NumericValue.HasValue
                ? PredictionCompletionStatus.Complete
                : PredictionCompletionStatus.NotStarted,
            PredictionQuestionType.NumericAthletePrediction => selectedCount switch
            {
                0 => PredictionCompletionStatus.NotStarted,
                2 => PredictionCompletionStatus.Complete,
                _ => PredictionCompletionStatus.InProgress
            },
            PredictionQuestionType.SingleAthleteChoice or PredictionQuestionType.MultipleChoice => selectedCount > 0
                ? PredictionCompletionStatus.Complete
                : PredictionCompletionStatus.NotStarted,
            PredictionQuestionType.MultiAthleteChoice or PredictionQuestionType.AthleteRanking or PredictionQuestionType.CategoryPodium
                => ResolveSelectionQuestionStatus(question, selectedCount),
            _ => PredictionCompletionStatus.NotStarted
        };

        if (status == PredictionCompletionStatus.Complete && questionErrors.Count > 0)
        {
            return PredictionCompletionStatus.InProgress;
        }

        return status;
    }

    private static PredictionCompletionStatus ResolveSelectionQuestionStatus(
        PredictionQuestion question,
        int selectedCount)
    {
        if (selectedCount == 0)
        {
            return PredictionCompletionStatus.NotStarted;
        }

        if (question.Constraints.ExactSelections is { } exactSelections)
        {
            return selectedCount == exactSelections
                ? PredictionCompletionStatus.Complete
                : PredictionCompletionStatus.InProgress;
        }

        if (question.Constraints.MinSelections is { } minSelections && selectedCount < minSelections)
        {
            return PredictionCompletionStatus.InProgress;
        }

        if (question.Constraints.MaxSelections is { } maxSelections && selectedCount > maxSelections)
        {
            return PredictionCompletionStatus.InProgress;
        }

        return PredictionCompletionStatus.Complete;
    }

    private static int GetSelectedCount(PredictionQuestion question, PredictionAnswer? answer)
    {
        if (answer is null)
        {
            return 0;
        }

        return question.Type switch
        {
            PredictionQuestionType.YesNo => answer.Value.BooleanValue.HasValue ? 1 : 0,
            PredictionQuestionType.NumericQuestion => answer.Value.NumericValue.HasValue ? 1 : 0,
            PredictionQuestionType.NumericAthletePrediction =>
                (string.IsNullOrWhiteSpace(answer.Value.SelectedAthleteId) ? 0 : 1) +
                (answer.Value.NumericValue.HasValue ? 1 : 0),
            PredictionQuestionType.SingleAthleteChoice => string.IsNullOrWhiteSpace(answer.Value.SelectedAthleteId) ? 0 : 1,
            PredictionQuestionType.MultiAthleteChoice => answer.Value.SelectedAthleteIds.Count,
            PredictionQuestionType.MultipleChoice => string.IsNullOrWhiteSpace(answer.Value.SelectedOptionId) ? 0 : 1,
            PredictionQuestionType.AthleteRanking or PredictionQuestionType.CategoryPodium =>
                answer.Value.AthletePlacements.Count(placement => !string.IsNullOrWhiteSpace(placement.AthleteId)),
            _ => 0
        };
    }

    private static int GetRequiredCount(PredictionQuestion question)
    {
        if (question.Constraints.ExactSelections is { } exactSelections)
        {
            return exactSelections;
        }

        if (question.Constraints.MinSelections is { } minSelections)
        {
            return minSelections;
        }

        return question.Type switch
        {
            PredictionQuestionType.NumericAthletePrediction => 2,
            PredictionQuestionType.YesNo or
            PredictionQuestionType.NumericQuestion or
            PredictionQuestionType.SingleAthleteChoice or
            PredictionQuestionType.MultipleChoice => 1,
            _ => 1
        };
    }

    private static void ValidateSelectionCount(
        PredictionGroup group,
        PredictionQuestion question,
        PredictionAnswer answer,
        List<PredictionValidationError> errors)
    {
        var count = question.Type switch
        {
            PredictionQuestionType.MultiAthleteChoice => answer.Value.SelectedAthleteIds.Count,
            PredictionQuestionType.AthleteRanking or PredictionQuestionType.CategoryPodium =>
                answer.Value.AthletePlacements.Count(placement => !string.IsNullOrWhiteSpace(placement.AthleteId)),
            _ => (int?)null
        };

        if (count is null)
        {
            return;
        }

        // Incomplete categories are tracked via completion status (not validation errors).
        // We only raise selection-count errors when the user exceeds configured limits.
        if (question.Constraints.ExactSelections is not null && count > question.Constraints.ExactSelections)
        {
            errors.Add(CreateError(
                group.Id,
                question.Id,
                "Validation.ExactSelections",
                $"Select exactly {question.Constraints.ExactSelections} item(s).",
                ("count", question.Constraints.ExactSelections.Value.ToString())));
        }

        if (question.Constraints.MaxSelections is not null && count > question.Constraints.MaxSelections)
        {
            errors.Add(CreateError(
                group.Id,
                question.Id,
                "Validation.MaxSelections",
                $"Select no more than {question.Constraints.MaxSelections} item(s).",
                ("count", question.Constraints.MaxSelections.Value.ToString())));
        }
    }

    private static void ValidateNumericRange(
        PredictionGroup group,
        PredictionQuestion question,
        PredictionAnswer answer,
        List<PredictionValidationError> errors)
    {
        if (answer.Value.NumericValue is not { } numericValue)
        {
            return;
        }

        if (question.Constraints.MinValue is not null && numericValue < question.Constraints.MinValue)
        {
            errors.Add(CreateError(
                group.Id,
                question.Id,
                "Validation.MinValue",
                $"Value must be at least {question.Constraints.MinValue}.",
                ("value", question.Constraints.MinValue.Value.ToString())));
        }

        if (question.Constraints.MaxValue is not null && numericValue > question.Constraints.MaxValue)
        {
            errors.Add(CreateError(
                group.Id,
                question.Id,
                "Validation.MaxValue",
                $"Value must be no more than {question.Constraints.MaxValue}.",
                ("value", question.Constraints.MaxValue.Value.ToString())));
        }
    }

    private static void ValidateDuplicateAthletes(
        PredictionGroup group,
        PredictionQuestion question,
        PredictionAnswer answer,
        List<PredictionValidationError> errors)
    {
        if (!question.Constraints.DisallowDuplicateAthletes)
        {
            return;
        }

        var athleteIds = question.Type switch
        {
            PredictionQuestionType.MultiAthleteChoice => answer.Value.SelectedAthleteIds,
            PredictionQuestionType.AthleteRanking or PredictionQuestionType.CategoryPodium =>
                answer.Value.AthletePlacements
                    .Select(placement => placement.AthleteId)
                    .Where(athleteId => !string.IsNullOrWhiteSpace(athleteId))
                    .ToArray(),
            _ => []
        };

        if (athleteIds.Count != athleteIds.Distinct(StringComparer.OrdinalIgnoreCase).Count())
        {
            errors.Add(CreateError(
                group.Id,
                question.Id,
                "Validation.DuplicateAthletes",
                "The same athlete cannot be selected more than once."));
        }
    }

    private static PredictionValidationError CreateError(
        string groupId,
        string questionId,
        string messageKey,
        string defaultMessage,
        params (string Key, string Value)[] parameters)
    {
        return new PredictionValidationError(groupId, questionId, defaultMessage)
        {
            MessageKey = messageKey,
            Parameters = parameters.ToDictionary(parameter => parameter.Key, parameter => parameter.Value)
        };
    }
}
