namespace TotalCall.Client.Domain.Predictions;

public static class PredictionModuleType
{
    public const string TopNByCategory = "top-n-by-category";
    public const string AthleteRanking = "athlete-ranking";
    public const string CategoryPodium = "category-podium";
    public const string NumericAthletePrediction = "numeric-athlete-prediction";
    public const string SingleAthleteChoice = "single-athlete-choice";
    public const string MultiAthleteChoice = "multi-athlete-choice";
    public const string YesNo = "yes-no";
    public const string MultipleChoice = "multiple-choice";
    public const string NumericQuestion = "numeric-question";

    public static string Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().ToLowerInvariant();
    }
}
