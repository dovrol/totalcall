using System.Text.Json.Serialization;

namespace TotalCall.Client.Domain.Predictions;

[JsonConverter(typeof(JsonStringEnumConverter<PredictionQuestionType>))]
public enum PredictionQuestionType
{
    [JsonStringEnumMemberName("athlete-ranking")]
    AthleteRanking = 0,

    [JsonStringEnumMemberName("category-podium")]
    CategoryPodium = 1,

    [JsonStringEnumMemberName("numeric-athlete-prediction")]
    NumericAthletePrediction = 2,

    [JsonStringEnumMemberName("single-athlete-choice")]
    SingleAthleteChoice = 3,

    [JsonStringEnumMemberName("multi-athlete-choice")]
    MultiAthleteChoice = 4,

    [JsonStringEnumMemberName("yes-no")]
    YesNo = 5,

    [JsonStringEnumMemberName("multiple-choice")]
    MultipleChoice = 6,

    [JsonStringEnumMemberName("numeric-question")]
    NumericQuestion = 7
}
