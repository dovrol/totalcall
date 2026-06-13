using System.Text.Json;
using TotalCall.Client.Domain.Competitions;
using TotalCall.Client.Infrastructure.Json;

namespace TotalCall.Tests.Domain.Competitions;

public sealed class CompetitionUpdateTests
{
    [Fact]
    public void Deserialize_defaults_missing_updates_to_empty_list()
    {
        var competition = JsonSerializer.Deserialize<Competition>(
            """
            {
              "id": "competition",
              "slug": "competition",
              "name": "Competition",
              "configVersion": "1"
            }
            """,
            JsonDataOptions.SerializerOptions);

        Assert.NotNull(competition);
        Assert.Empty(competition.Updates);
    }

    [Fact]
    public void Deserialize_reads_timeline_updates_with_snake_case_fields()
    {
        var competition = JsonSerializer.Deserialize<Competition>(
            """
            {
              "id": "competition",
              "slug": "competition",
              "name": "Competition",
              "configVersion": "1",
              "updates": [
                {
                  "id": "roster-1",
                  "type": "roster_update",
                  "occurred_at": "2026-06-12T12:00:00Z",
                  "title": "Roster update: Athlete One has withdrawn.",
                  "body": "Athlete One is no longer selectable.",
                  "athlete_ids": ["athlete-1"],
                  "source": "federation"
                }
              ]
            }
            """,
            JsonDataOptions.SerializerOptions);

        Assert.NotNull(competition);

        var update = Assert.Single(competition.Updates);
        Assert.Equal("roster-1", update.Id);
        Assert.Equal(CompetitionUpdateTypes.RosterUpdate, update.Type);
        Assert.Equal(DateTimeOffset.Parse("2026-06-12T12:00:00Z"), update.OccurredAt);
        Assert.Equal("Roster update: Athlete One has withdrawn.", update.Title);
        Assert.Equal("Athlete One is no longer selectable.", update.Body);
        Assert.Equal(["athlete-1"], update.AthleteIds);
        Assert.Equal("federation", update.Source);
    }

    [Fact]
    public void Deserialize_defaults_missing_update_optional_fields()
    {
        var competition = JsonSerializer.Deserialize<Competition>(
            """
            {
              "id": "competition",
              "slug": "competition",
              "name": "Competition",
              "configVersion": "1",
              "updates": [
                {
                  "title": "Short update"
                }
              ]
            }
            """,
            JsonDataOptions.SerializerOptions);

        Assert.NotNull(competition);

        var update = Assert.Single(competition.Updates);
        Assert.Null(update.Id);
        Assert.Equal(CompetitionUpdateTypes.General, update.Type);
        Assert.Null(update.OccurredAt);
        Assert.Equal("Short update", update.Title);
        Assert.Null(update.Body);
        Assert.Empty(update.AthleteIds);
        Assert.Null(update.Source);
    }

    [Fact]
    public void Normalize_update_type_trims_and_lowercases_values()
    {
        Assert.Equal(CompetitionUpdateTypes.General, CompetitionUpdateTypes.Normalize(null));
        Assert.Equal(CompetitionUpdateTypes.General, CompetitionUpdateTypes.Normalize(" "));
        Assert.Equal(CompetitionUpdateTypes.RosterUpdate, CompetitionUpdateTypes.Normalize(" ROSTER_UPDATE "));
    }
}
