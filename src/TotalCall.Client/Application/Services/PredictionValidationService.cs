using TotalCall.Client.Domain.Competitions;
using TotalCall.Client.Domain.Predictions;

namespace TotalCall.Client.Application.Services;

public sealed class PredictionValidationService
{
    public PredictionValidationResult Validate(Competition competition, PredictionSet predictionSet)
    {
        var errors = new List<PredictionValidationError>();

        foreach (var group in competition.PredictionGroups)
        {
            foreach (var question in group.Questions)
            {
                var answer = predictionSet.Answers.FirstOrDefault(candidate => candidate.QuestionId == question.Id);
                ValidateQuestion(group, question, answer, errors);
            }
        }

        return new PredictionValidationResult(errors);
    }

    private static void ValidateQuestion(
        PredictionGroup group,
        PredictionQuestion question,
        PredictionAnswer? answer,
        List<PredictionValidationError> errors)
    {
        if (question.Required && !IsAnswered(question, answer))
        {
            errors.Add(new PredictionValidationError(group.Id, question.Id, "This question is required."));
            return;
        }

        if (answer is null)
        {
            return;
        }

        ValidateSelectionCount(group, question, answer, errors);
        ValidateNumericRange(group, question, answer, errors);
        ValidateDuplicateAthletes(group, question, answer, errors);
    }

    private static bool IsAnswered(PredictionQuestion question, PredictionAnswer? answer)
    {
        if (answer is null)
        {
            return false;
        }

        return question.Type switch
        {
            PredictionQuestionType.YesNo => answer.Value.BooleanValue.HasValue,
            PredictionQuestionType.NumericQuestion => answer.Value.NumericValue.HasValue,
            PredictionQuestionType.NumericAthletePrediction =>
                !string.IsNullOrWhiteSpace(answer.Value.SelectedAthleteId) &&
                answer.Value.NumericValue.HasValue,
            PredictionQuestionType.SingleAthleteChoice => !string.IsNullOrWhiteSpace(answer.Value.SelectedAthleteId),
            PredictionQuestionType.MultiAthleteChoice => answer.Value.SelectedAthleteIds.Count > 0,
            PredictionQuestionType.MultipleChoice => !string.IsNullOrWhiteSpace(answer.Value.SelectedOptionId),
            PredictionQuestionType.AthleteRanking or PredictionQuestionType.CategoryPodium =>
                answer.Value.AthletePlacements.Count > 0 &&
                answer.Value.AthletePlacements.All(placement => !string.IsNullOrWhiteSpace(placement.AthleteId)),
            _ => false
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
            PredictionQuestionType.AthleteRanking or PredictionQuestionType.CategoryPodium => answer.Value.AthletePlacements.Count,
            _ => (int?)null
        };

        if (count is null)
        {
            return;
        }

        if (question.Constraints.ExactSelections is not null && count != question.Constraints.ExactSelections)
        {
            errors.Add(new PredictionValidationError(
                group.Id,
                question.Id,
                $"Select exactly {question.Constraints.ExactSelections} item(s)."));
        }

        if (question.Constraints.MinSelections is not null && count < question.Constraints.MinSelections)
        {
            errors.Add(new PredictionValidationError(
                group.Id,
                question.Id,
                $"Select at least {question.Constraints.MinSelections} item(s)."));
        }

        if (question.Constraints.MaxSelections is not null && count > question.Constraints.MaxSelections)
        {
            errors.Add(new PredictionValidationError(
                group.Id,
                question.Id,
                $"Select no more than {question.Constraints.MaxSelections} item(s)."));
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
            errors.Add(new PredictionValidationError(
                group.Id,
                question.Id,
                $"Value must be at least {question.Constraints.MinValue}."));
        }

        if (question.Constraints.MaxValue is not null && numericValue > question.Constraints.MaxValue)
        {
            errors.Add(new PredictionValidationError(
                group.Id,
                question.Id,
                $"Value must be no more than {question.Constraints.MaxValue}."));
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
                answer.Value.AthletePlacements.Select(placement => placement.AthleteId).ToArray(),
            _ => []
        };

        if (athleteIds.Count != athleteIds.Distinct(StringComparer.OrdinalIgnoreCase).Count())
        {
            errors.Add(new PredictionValidationError(group.Id, question.Id, "The same athlete cannot be selected more than once."));
        }
    }
}
