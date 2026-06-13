using System.Text.Json.Serialization;

namespace TotalCall.Client.Domain.Athletes;

[JsonConverter(typeof(JsonStringEnumConverter<AthleteStatus>))]
public enum AthleteStatus
{
    [JsonStringEnumMemberName("active")]
    Active = 0,

    [JsonStringEnumMemberName("withdrawn")]
    Withdrawn = 1
}
