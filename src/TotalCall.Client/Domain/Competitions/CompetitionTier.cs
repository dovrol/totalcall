using System.Text.Json.Serialization;

namespace TotalCall.Client.Domain.Competitions;

[JsonConverter(typeof(JsonStringEnumConverter<CompetitionTier>))]
public enum CompetitionTier
{
    [JsonStringEnumMemberName("s")]
    S = 0,

    [JsonStringEnumMemberName("a")]
    A = 1,

    [JsonStringEnumMemberName("b")]
    B = 2
}
