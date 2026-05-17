using TotalCall.Client.Domain.Competitions;
using TotalCall.Client.Domain.Predictions;

namespace TotalCall.Client.Application.Services;

public sealed class PredictionAnswerDisplayService
{
    public string FormatAnswer(Competition competition, PredictionQuestion question, PredictionAnswer? answer)
    {
        if (answer is null)
        {
            return "Not answered";
        }

        return question.Type switch
        {
            PredictionQuestionType.YesNo => FormatYesNo(answer),
            PredictionQuestionType.NumericQuestion => FormatNumeric(question, answer),
            PredictionQuestionType.NumericAthletePrediction => FormatNumericAthlete(competition, question, answer),
            PredictionQuestionType.MultipleChoice => FormatMultipleChoice(question, answer),
            PredictionQuestionType.SingleAthleteChoice => FormatAthlete(competition, answer.Value.SelectedAthleteId),
            PredictionQuestionType.MultiAthleteChoice => FormatAthletes(competition, answer.Value.SelectedAthleteIds),
            PredictionQuestionType.AthleteRanking => FormatPlacements(competition, answer, useMedalLabels: false),
            PredictionQuestionType.CategoryPodium => FormatPlacements(competition, answer, useMedalLabels: true),
            _ => "Unsupported answer"
        };
    }

    private static string FormatYesNo(PredictionAnswer answer)
    {
        return answer.Value.BooleanValue switch
        {
            true => "Yes",
            false => "No",
            null => "Not answered"
        };
    }

    private static string FormatNumeric(PredictionQuestion question, PredictionAnswer answer)
    {
        return answer.Value.NumericValue is null
            ? "Not answered"
            : $"{answer.Value.NumericValue}{FormatUnit(question)}";
    }

    private static string FormatNumericAthlete(
        Competition competition,
        PredictionQuestion question,
        PredictionAnswer answer)
    {
        var athlete = FormatAthlete(competition, answer.Value.SelectedAthleteId);
        var numericValue = answer.Value.NumericValue is null
            ? "no value"
            : $"{answer.Value.NumericValue}{FormatUnit(question)}";

        return $"{athlete}, {numericValue}";
    }

    private static string FormatMultipleChoice(PredictionQuestion question, PredictionAnswer answer)
    {
        var option = question.Options.FirstOrDefault(option => option.Id == answer.Value.SelectedOptionId);

        return option?.Label ?? "Not answered";
    }

    private static string FormatAthletes(Competition competition, IReadOnlyList<string> athleteIds)
    {
        return athleteIds.Count == 0
            ? "Not answered"
            : string.Join(", ", athleteIds.Select(athleteId => FormatAthlete(competition, athleteId)));
    }

    private static string FormatPlacements(Competition competition, PredictionAnswer answer, bool useMedalLabels)
    {
        if (answer.Value.AthletePlacements.Count == 0)
        {
            return "Not answered";
        }

        return string.Join(
            ", ",
            answer.Value.AthletePlacements
                .OrderBy(placement => placement.Position)
                .Select(placement => $"{FormatPosition(placement.Position, useMedalLabels)} {FormatAthlete(competition, placement.AthleteId)}"));
    }

    private static string FormatAthlete(Competition competition, string? athleteId)
    {
        if (string.IsNullOrWhiteSpace(athleteId))
        {
            return "Not answered";
        }

        return competition.Athletes.FirstOrDefault(athlete => athlete.Id == athleteId)?.DisplayName ?? athleteId;
    }

    private static string FormatPosition(int position, bool useMedalLabels)
    {
        if (!useMedalLabels)
        {
            return $"#{position}";
        }

        return position switch
        {
            1 => "Gold:",
            2 => "Silver:",
            3 => "Bronze:",
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
