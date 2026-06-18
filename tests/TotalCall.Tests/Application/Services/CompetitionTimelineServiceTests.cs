using TotalCall.Client.Application.Services;
using TotalCall.Core.Domain.Athletes;
using TotalCall.Core.Domain.Competitions;

namespace TotalCall.Tests.Application.Services;

public sealed class CompetitionTimelineServiceTests
{
    [Fact]
    public void GetTimeline_generates_roster_update_from_withdrawn_athlete()
    {
        var withdrawnAt = DateTimeOffset.Parse("2026-06-12T12:00:00Z");
        var competition = CreateCompetition(
            athletes:
            [
                new Athlete
                {
                    Id = "withdrawn",
                    DisplayName = "Withdrawn Athlete",
                    Status = AthleteStatus.Withdrawn,
                    WithdrawnAt = withdrawnAt,
                    WithdrawalReason = "Injury",
                    WithdrawalSource = "federation"
                }
            ]);

        var timeline = CompetitionTimelineService.GetTimeline(competition);

        var item = Assert.Single(timeline);
        Assert.True(item.IsGenerated);
        Assert.Equal(CompetitionUpdateTypes.RosterUpdate, item.Type);
        Assert.Equal(withdrawnAt, item.OccurredAt);
        Assert.Equal("Injury", item.Body);
        Assert.Equal("federation", item.Source);
        Assert.Equal("Withdrawn Athlete", Assert.Single(item.Athletes).DisplayName);
    }

    [Fact]
    public void GetTimeline_does_not_duplicate_manual_roster_update_for_same_athlete()
    {
        var competition = CreateCompetition(
            updates:
            [
                new CompetitionUpdate
                {
                    Id = "manual-roster",
                    Type = CompetitionUpdateTypes.RosterUpdate,
                    OccurredAt = DateTimeOffset.Parse("2026-06-12T12:00:00Z"),
                    Title = "Manual roster update",
                    AthleteIds = ["withdrawn"]
                }
            ],
            athletes:
            [
                new Athlete
                {
                    Id = "withdrawn",
                    DisplayName = "Withdrawn Athlete",
                    Status = AthleteStatus.Withdrawn,
                    WithdrawnAt = DateTimeOffset.Parse("2026-06-12T12:00:00Z")
                }
            ]);

        var timeline = CompetitionTimelineService.GetTimeline(competition);

        var item = Assert.Single(timeline);
        Assert.False(item.IsGenerated);
        Assert.Equal("manual-roster", item.Id);
        Assert.Equal("Manual roster update", item.Title);
    }

    [Fact]
    public void GetTimeline_generates_roster_update_for_withdrawn_athletes_not_covered_by_manual_update()
    {
        var competition = CreateCompetition(
            updates:
            [
                new CompetitionUpdate
                {
                    Id = "manual-roster",
                    Type = CompetitionUpdateTypes.RosterUpdate,
                    OccurredAt = DateTimeOffset.Parse("2026-06-12T12:00:00Z"),
                    Title = "Manual roster update",
                    AthleteIds = ["withdrawn-1"]
                }
            ],
            athletes:
            [
                new Athlete
                {
                    Id = "withdrawn-1",
                    DisplayName = "First Withdrawn",
                    Status = AthleteStatus.Withdrawn,
                    WithdrawnAt = DateTimeOffset.Parse("2026-06-12T12:00:00Z")
                },
                new Athlete
                {
                    Id = "withdrawn-2",
                    DisplayName = "Second Withdrawn",
                    Status = AthleteStatus.Withdrawn,
                    WithdrawnAt = DateTimeOffset.Parse("2026-06-13T12:00:00Z")
                }
            ]);

        var timeline = CompetitionTimelineService.GetTimeline(competition);

        Assert.Equal(["generated-roster-withdrawn-withdrawn-2", "manual-roster"], timeline.Select(item => item.Id));
        Assert.False(timeline.Single(item => item.Id == "manual-roster").IsGenerated);
        Assert.True(timeline.Single(item => item.Id == "generated-roster-withdrawn-withdrawn-2").IsGenerated);
    }

    [Fact]
    public void GetTimeline_resolves_manual_update_athletes_case_insensitively_and_ignores_unknown_ids()
    {
        var competition = CreateCompetition(
            updates:
            [
                new CompetitionUpdate
                {
                    Id = "manual-roster",
                    Type = " ROSTER_UPDATE ",
                    AthleteIds = ["WITHDRAWN", "withdrawn", "", "unknown"]
                }
            ],
            athletes:
            [
                new Athlete
                {
                    Id = "withdrawn",
                    DisplayName = "Withdrawn Athlete",
                    Status = AthleteStatus.Withdrawn
                }
            ]);

        var item = Assert.Single(CompetitionTimelineService.GetTimeline(competition));

        Assert.Equal(CompetitionUpdateTypes.RosterUpdate, item.Type);
        Assert.Equal("Withdrawn Athlete", Assert.Single(item.Athletes).DisplayName);
    }

    [Fact]
    public void GetTimeline_builds_stable_id_for_manual_update_without_id()
    {
        var occurredAt = DateTimeOffset.Parse("2026-06-12T12:00:00Z");
        var competition = CreateCompetition(
            updates:
            [
                new CompetitionUpdate
                {
                    Type = " results_update ",
                    OccurredAt = occurredAt,
                    Title = "Results update"
                }
            ]);

        var item = Assert.Single(CompetitionTimelineService.GetTimeline(competition));

        Assert.Equal($"results_update-{occurredAt.ToUnixTimeSeconds()}-0", item.Id);
        Assert.Equal(CompetitionUpdateTypes.ResultsUpdate, item.Type);
        Assert.False(item.IsGenerated);
    }

    [Fact]
    public void GetTimeline_keeps_first_manual_update_when_ids_are_duplicated()
    {
        var competition = CreateCompetition(
            updates:
            [
                new CompetitionUpdate
                {
                    Id = "duplicate",
                    Type = CompetitionUpdateTypes.General,
                    OccurredAt = DateTimeOffset.Parse("2026-06-10T10:00:00Z"),
                    Title = "First update"
                },
                new CompetitionUpdate
                {
                    Id = "DUPLICATE",
                    Type = CompetitionUpdateTypes.ResultsUpdate,
                    OccurredAt = DateTimeOffset.Parse("2026-06-13T10:00:00Z"),
                    Title = "Second update"
                }
            ]);

        var item = Assert.Single(CompetitionTimelineService.GetTimeline(competition));

        Assert.Equal("duplicate", item.Id);
        Assert.Equal("First update", item.Title);
        Assert.Equal(CompetitionUpdateTypes.General, item.Type);
    }

    [Fact]
    public void GetTimeline_sorts_same_timestamp_by_update_priority_and_title()
    {
        var occurredAt = DateTimeOffset.Parse("2026-06-12T12:00:00Z");
        var competition = CreateCompetition(
            updates:
            [
                new CompetitionUpdate { Id = "general-b", Type = CompetitionUpdateTypes.General, OccurredAt = occurredAt, Title = "B update" },
                new CompetitionUpdate { Id = "scoring", Type = CompetitionUpdateTypes.ScoringUpdate, OccurredAt = occurredAt, Title = "Scoring update" },
                new CompetitionUpdate { Id = "results", Type = CompetitionUpdateTypes.ResultsUpdate, OccurredAt = occurredAt, Title = "Results update" },
                new CompetitionUpdate { Id = "deadline", Type = CompetitionUpdateTypes.DeadlineChange, OccurredAt = occurredAt, Title = "Deadline update" },
                new CompetitionUpdate { Id = "general-a", Type = CompetitionUpdateTypes.General, OccurredAt = occurredAt, Title = "A update" }
            ],
            athletes:
            [
                new Athlete
                {
                    Id = "withdrawn",
                    DisplayName = "Withdrawn Athlete",
                    Status = AthleteStatus.Withdrawn,
                    WithdrawnAt = occurredAt
                }
            ]);

        var timeline = CompetitionTimelineService.GetTimeline(competition);

        Assert.Equal(
            ["generated-roster-withdrawn-withdrawn", "deadline", "results", "scoring", "general-a", "general-b"],
            timeline.Select(item => item.Id));
    }

    [Fact]
    public void GetTimeline_places_undated_items_after_dated_items()
    {
        var competition = CreateCompetition(
            updates:
            [
                new CompetitionUpdate
                {
                    Id = "undated",
                    Title = "Undated update"
                },
                new CompetitionUpdate
                {
                    Id = "dated",
                    OccurredAt = DateTimeOffset.Parse("2026-06-12T12:00:00Z"),
                    Title = "Dated update"
                }
            ]);

        var timeline = CompetitionTimelineService.GetTimeline(competition);

        Assert.Equal(["dated", "undated"], timeline.Select(item => item.Id));
    }

    [Fact]
    public void GetTimeline_keeps_manual_updates_and_sorts_latest_first()
    {
        var competition = CreateCompetition(
            updates:
            [
                new CompetitionUpdate
                {
                    Id = "deadline",
                    Type = CompetitionUpdateTypes.DeadlineChange,
                    OccurredAt = DateTimeOffset.Parse("2026-06-10T10:00:00Z"),
                    Title = "Deadline changed"
                },
                new CompetitionUpdate
                {
                    Id = "results",
                    Type = CompetitionUpdateTypes.ResultsUpdate,
                    OccurredAt = DateTimeOffset.Parse("2026-06-13T10:00:00Z"),
                    Title = "Results published"
                }
            ],
            athletes:
            [
                new Athlete
                {
                    Id = "withdrawn",
                    DisplayName = "Withdrawn Athlete",
                    Status = AthleteStatus.Withdrawn,
                    WithdrawnAt = DateTimeOffset.Parse("2026-06-12T12:00:00Z")
                }
            ]);

        var timeline = CompetitionTimelineService.GetTimeline(competition);

        Assert.Equal(["results", "generated-roster-withdrawn-withdrawn", "deadline"], timeline.Select(item => item.Id));
    }

    private static Competition CreateCompetition(
        IReadOnlyList<CompetitionUpdate>? updates = null,
        IReadOnlyList<Athlete>? athletes = null)
    {
        return new Competition
        {
            Id = "competition",
            Slug = "competition",
            Name = "Competition",
            ConfigVersion = "1",
            Updates = updates ?? [],
            Athletes = athletes ?? []
        };
    }
}
