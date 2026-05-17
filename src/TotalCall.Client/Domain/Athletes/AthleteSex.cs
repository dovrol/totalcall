using System.Text.Json.Serialization;

namespace TotalCall.Client.Domain.Athletes;

[JsonConverter(typeof(JsonStringEnumConverter<AthleteSex>))]
public enum AthleteSex
{
    [JsonStringEnumMemberName("unspecified")]
    Unspecified = 0,

    [JsonStringEnumMemberName("female")]
    Female = 1,

    [JsonStringEnumMemberName("male")]
    Male = 2
}
