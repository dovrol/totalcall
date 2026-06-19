using System.Text.Json.Nodes;
using TotalCall.Operations.Competitions;

namespace TotalCall.Tests.Tools;

public sealed class CompetitionConfigHasherTests
{
    [Fact]
    public void Compute_TreatsEquivalentJsonAsSameConfig()
    {
        var first = JsonNode.Parse(
            """
            {
              "id": "worlds-2026",
              "configVersion": "1",
              "predictionGroups": [
                { "id": "total", "order": 1 }
              ]
            }
            """)!;
        var second = JsonNode.Parse(
            """
            {"predictionGroups":[{"order":1,"id":"total"}],"configVersion":"1","id":"worlds-2026"}
            """)!;

        Assert.Equal(
            CompetitionConfigHasher.Compute(first),
            CompetitionConfigHasher.Compute(second));
    }

    [Fact]
    public void Compute_ChangesWhenConfigContentChanges()
    {
        var first = JsonNode.Parse(
            """
            {
              "id": "worlds-2026",
              "configVersion": "1",
              "predictionGroups": [
                { "id": "total", "order": 1 }
              ]
            }
            """)!;
        var second = JsonNode.Parse(
            """
            {
              "id": "worlds-2026",
              "configVersion": "1",
              "predictionGroups": [
                { "id": "total", "order": 2 }
              ]
            }
            """)!;

        Assert.NotEqual(
            CompetitionConfigHasher.Compute(first),
            CompetitionConfigHasher.Compute(second));
    }

    [Fact]
    public void Compute_ChangesWhenAthleteStatusChangesToWithdrawn()
    {
        var active = JsonNode.Parse(
            """
            {
              "id": "worlds-2026",
              "configVersion": "1",
              "athletes": [
                { "id": "a1", "displayName": "Athlete One", "status": "active" }
              ]
            }
            """)!;
        var withdrawn = JsonNode.Parse(
            """
            {
              "id": "worlds-2026",
              "configVersion": "1",
              "athletes": [
                { "id": "a1", "displayName": "Athlete One", "status": "withdrawn" }
              ]
            }
            """)!;

        Assert.NotEqual(
            CompetitionConfigHasher.Compute(active),
            CompetitionConfigHasher.Compute(withdrawn));
    }

    [Fact]
    public void Compute_ChangesWhenCompetitionTimelineChanges()
    {
        var first = JsonNode.Parse(
            """
            {
              "id": "worlds-2026",
              "configVersion": "1",
              "updates": [
                { "id": "roster-1", "type": "roster_update", "title": "Roster update" }
              ]
            }
            """)!;
        var second = JsonNode.Parse(
            """
            {
              "id": "worlds-2026",
              "configVersion": "1",
              "updates": [
                { "id": "roster-1", "type": "roster_update", "title": "Roster update changed" }
              ]
            }
            """)!;

        Assert.NotEqual(
            CompetitionConfigHasher.Compute(first),
            CompetitionConfigHasher.Compute(second));
    }
}
