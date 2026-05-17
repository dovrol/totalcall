using System.Text.Json.Serialization;

namespace TotalCall.Client.Domain.Competitions;

[JsonConverter(typeof(JsonStringEnumConverter<CompetitionStatus>))]
public enum CompetitionStatus
{
    [JsonStringEnumMemberName("upcoming")]
    Upcoming = 0,

    [JsonStringEnumMemberName("locked")]
    Locked = 1,

    [JsonStringEnumMemberName("completed")]
    Completed = 2,

    [JsonStringEnumMemberName("archived")]
    Archived = 3
}
