using Microsoft.Extensions.Localization;
using System.Globalization;
using TotalCall.Core.Domain.Competitions;
using TotalCall.Core.Domain.Predictions;

namespace TotalCall.Client.Application.Services;

public sealed class PredictionAnswerDisplayService(
    IStringLocalizer<SharedResource> localizer,
    PredictionTextService text)
{
    public string FormatAnswer(Competition competition, PredictionQuestion question, PredictionAnswer? answer)
    {
        if (answer is null)
        {
            return localizer["Common.NotAnswered"];
        }

        return question.Type switch
        {
            PredictionQuestionType.YesNo => FormatYesNo(answer),
            PredictionQuestionType.NumericQuestion => FormatNumeric(question, answer),
            PredictionQuestionType.NumericAthletePrediction => FormatNumericAthlete(competition, question, answer),
            PredictionQuestionType.MultipleChoice => FormatMultipleChoice(competition, question, answer),
            PredictionQuestionType.SingleAthleteChoice => FormatAthlete(competition, answer.Value.SelectedAthleteId),
            PredictionQuestionType.MultiAthleteChoice => FormatAthletes(competition, answer.Value.SelectedAthleteIds),
            PredictionQuestionType.AthleteRanking => FormatPlacements(competition, answer, useMedalLabels: false),
            PredictionQuestionType.CategoryPodium => FormatPlacements(competition, answer, useMedalLabels: true),
            _ => localizer["Common.NotAnswered"]
        };
    }

    private string FormatYesNo(PredictionAnswer answer)
    {
        return answer.Value.BooleanValue switch
        {
            true => localizer["Common.Yes"],
            false => localizer["Common.No"],
            null => localizer["Common.NotAnswered"]
        };
    }

    private string FormatNumeric(PredictionQuestion question, PredictionAnswer answer)
    {
        return answer.Value.NumericValue is null
            ? localizer["Common.NotAnswered"]
            : $"{answer.Value.NumericValue}{FormatUnit(question)}";
    }

    private string FormatNumericAthlete(
        Competition competition,
        PredictionQuestion question,
        PredictionAnswer answer)
    {
        var athlete = FormatAthlete(competition, answer.Value.SelectedAthleteId);
        var numericValue = answer.Value.NumericValue is null
            ? localizer["Common.NoValue"]
            : $"{answer.Value.NumericValue}{FormatUnit(question)}";

        return $"{athlete}, {numericValue}";
    }

    private string FormatMultipleChoice(Competition competition, PredictionQuestion question, PredictionAnswer answer)
    {
        var option = question.Options.FirstOrDefault(option => option.Id == answer.Value.SelectedOptionId);

        return option is null
            ? localizer["Common.NotAnswered"]
            : text.OptionLabel(competition, question, option);
    }

    private string FormatAthletes(Competition competition, IReadOnlyList<string> athleteIds)
    {
        return athleteIds.Count == 0
            ? localizer["Common.NotAnswered"]
            : string.Join(", ", athleteIds.Select(athleteId => FormatAthlete(competition, athleteId)));
    }

    private string FormatPlacements(Competition competition, PredictionAnswer answer, bool useMedalLabels)
    {
        if (answer.Value.AthletePlacements.Count == 0)
        {
            return localizer["Common.NotAnswered"];
        }

        return string.Join(
            ", ",
            answer.Value.AthletePlacements
                .Where(placement => placement.IsScored)
                .OrderBy(placement => placement.Position)
                .Select(placement => $"{FormatPosition(placement.Position, useMedalLabels)} {FormatAthlete(competition, placement.AthleteId)}{FormatPlacementPrediction(placement)}"));
    }

    private static string FormatPlacementPrediction(AthletePlacementPick placement)
    {
        var parts = new List<string>();

        if (placement.PredictedTotalKg is not null)
        {
            parts.Add($"predicted {placement.PredictedTotalKg.Value.ToString("0.#", CultureInfo.CurrentCulture)} kg");
        }

        var liftParts = new List<string>();
        if (placement.PredictedSquatKg is not null)
        {
            liftParts.Add($"SQ {placement.PredictedSquatKg.Value.ToString("0.#", CultureInfo.CurrentCulture)}");
        }

        if (placement.PredictedBenchKg is not null)
        {
            liftParts.Add($"BP {placement.PredictedBenchKg.Value.ToString("0.#", CultureInfo.CurrentCulture)}");
        }

        if (placement.PredictedDeadliftKg is not null)
        {
            liftParts.Add($"DL {placement.PredictedDeadliftKg.Value.ToString("0.#", CultureInfo.CurrentCulture)}");
        }

        if (liftParts.Count > 0)
        {
            parts.Add(string.Join(" / ", liftParts));
        }

        return parts.Count == 0 ? string.Empty : $" ({string.Join(", ", parts)})";
    }

    private string FormatAthlete(Competition competition, string? athleteId)
    {
        if (string.IsNullOrWhiteSpace(athleteId))
        {
            return localizer["Common.NotAnswered"];
        }

        var athlete = competition.Athletes.FirstOrDefault(athlete => athlete.Id == athleteId);
        if (athlete is null)
        {
            return athleteId;
        }

        return athlete.IsWithdrawn
            ? $"{athlete.DisplayName} ({localizer["Predictions.Roster.Withdrawn"]})"
            : athlete.DisplayName;
    }

    private string FormatPosition(int position, bool useMedalLabels)
    {
        if (!useMedalLabels)
        {
            return $"#{position}";
        }

        return position switch
        {
            1 => $"{localizer["Common.Gold"]}:",
            2 => $"{localizer["Common.Silver"]}:",
            3 => $"{localizer["Common.Bronze"]}:",
            _ => $"#{position}:"
        };
    }

    private static string FormatUnit(PredictionQuestion question)
    {
        return string.IsNullOrWhiteSpace(question.Constraints.Unit)
            ? string.Empty
            : $" {question.Constraints.Unit}";
    }
}
