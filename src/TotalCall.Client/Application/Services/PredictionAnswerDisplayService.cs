using TotalCall.Client.Application.Localization;
using TotalCall.Client.Domain.Competitions;
using TotalCall.Client.Domain.Predictions;

namespace TotalCall.Client.Application.Services;

public sealed class PredictionAnswerDisplayService(LanguageService language)
{
    public string FormatAnswer(Competition competition, PredictionQuestion question, PredictionAnswer? answer)
    {
        if (answer is null)
        {
            return language.Text("Common.NotAnswered");
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
            _ => language.Text("Common.NotAnswered")
        };
    }

    private string FormatYesNo(PredictionAnswer answer)
    {
        return answer.Value.BooleanValue switch
        {
            true => language.Text("Common.Yes"),
            false => language.Text("Common.No"),
            null => language.Text("Common.NotAnswered")
        };
    }

    private string FormatNumeric(PredictionQuestion question, PredictionAnswer answer)
    {
        return answer.Value.NumericValue is null
            ? language.Text("Common.NotAnswered")
            : $"{answer.Value.NumericValue}{FormatUnit(question)}";
    }

    private string FormatNumericAthlete(
        Competition competition,
        PredictionQuestion question,
        PredictionAnswer answer)
    {
        var athlete = FormatAthlete(competition, answer.Value.SelectedAthleteId);
        var numericValue = answer.Value.NumericValue is null
            ? language.Text("Common.NoValue")
            : $"{answer.Value.NumericValue}{FormatUnit(question)}";

        return $"{athlete}, {numericValue}";
    }

    private string FormatMultipleChoice(PredictionQuestion question, PredictionAnswer answer)
    {
        var option = question.Options.FirstOrDefault(option => option.Id == answer.Value.SelectedOptionId);

        return option?.Label ?? language.Text("Common.NotAnswered");
    }

    private string FormatAthletes(Competition competition, IReadOnlyList<string> athleteIds)
    {
        return athleteIds.Count == 0
            ? language.Text("Common.NotAnswered")
            : string.Join(", ", athleteIds.Select(athleteId => FormatAthlete(competition, athleteId)));
    }

    private string FormatPlacements(Competition competition, PredictionAnswer answer, bool useMedalLabels)
    {
        if (answer.Value.AthletePlacements.Count == 0)
        {
            return language.Text("Common.NotAnswered");
        }

        return string.Join(
            ", ",
            answer.Value.AthletePlacements
                .OrderBy(placement => placement.Position)
                .Select(placement => $"{FormatPosition(placement.Position, useMedalLabels)} {FormatAthlete(competition, placement.AthleteId)}"));
    }

    private string FormatAthlete(Competition competition, string? athleteId)
    {
        if (string.IsNullOrWhiteSpace(athleteId))
        {
            return language.Text("Common.NotAnswered");
        }

        return competition.Athletes.FirstOrDefault(athlete => athlete.Id == athleteId)?.DisplayName ?? athleteId;
    }

    private string FormatPosition(int position, bool useMedalLabels)
    {
        if (!useMedalLabels)
        {
            return $"#{position}";
        }

        return position switch
        {
            1 => $"{language.Text("Common.Gold")}:",
            2 => $"{language.Text("Common.Silver")}:",
            3 => $"{language.Text("Common.Bronze")}:",
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
