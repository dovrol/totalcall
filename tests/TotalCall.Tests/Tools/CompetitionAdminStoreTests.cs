using System.Text.Json.Nodes;
using TotalCall.Operations.Competitions;

namespace TotalCall.Tests.Tools;

public sealed class CompetitionAdminStoreTests
{
    [Fact]
    public void BuildRows_resolves_active_version_latest_version_and_results()
    {
        var competitions = new JsonArray
        {
            new JsonObject
            {
                ["id"] = "worlds-2026",
                ["slug"] = "worlds-2026",
                ["name"] = "Worlds 2026",
                ["status"] = "locked",
                ["start_date"] = "2026-06-01T00:00:00Z",
                ["end_date"] = "2026-06-05T00:00:00Z",
                ["prediction_open_at"] = "2026-05-01T00:00:00Z",
                ["prediction_lock_at"] = "2026-05-31T23:59:00Z",
                ["published_version_id"] = "ver-1",
                ["updated_at"] = "2026-06-02T00:00:00Z"
            }
        };

        var versions = new JsonArray
        {
            new JsonObject
            {
                ["id"] = "ver-1",
                ["competition_id"] = "worlds-2026",
                ["version"] = "v1",
                ["published_at"] = "2026-05-10T00:00:00Z",
                ["created_at"] = "2026-05-09T00:00:00Z"
            },
            new JsonObject
            {
                ["id"] = "ver-2",
                ["competition_id"] = "worlds-2026",
                ["version"] = "v2",
                ["published_at"] = null,
                ["created_at"] = "2026-05-20T00:00:00Z"
            }
        };

        var results = new JsonArray
        {
            new JsonObject
            {
                ["competition_id"] = "worlds-2026",
                ["status"] = "final",
                ["imported_at"] = "2026-06-06T00:00:00Z",
                ["updated_at"] = "2026-06-06T00:00:00Z"
            }
        };

        var rows = CompetitionAdminStore.BuildRows(competitions, versions, results);

        var row = Assert.Single(rows);
        Assert.Equal("worlds-2026", row.Id);
        Assert.Equal("locked", row.Status);
        // Active version is resolved from published_version_id, not "latest".
        Assert.Equal("v1", row.ActiveVersion);
        Assert.Equal(2, row.VersionsCount);
        // Latest is by created_at, which is v2.
        Assert.Equal("v2", row.LatestVersion);
        Assert.Equal(DateTimeOffset.Parse("2026-05-20T00:00:00Z"), row.LatestVersionCreatedAt);
        Assert.Equal("final", row.OfficialResultsStatus);
        Assert.Equal(DateTimeOffset.Parse("2026-06-06T00:00:00Z"), row.OfficialResultsImportedAt);
        Assert.Equal(DateTimeOffset.Parse("2026-05-31T23:59:00Z"), row.PredictionLockAt);
        // Score snapshot fields are intentionally not queried for the grid.
        Assert.Null(row.ScoreSnapshotStatus);
        Assert.Null(row.ScoreSnapshotCalculatedAt);
    }

    [Fact]
    public void BuildRows_handles_unpublished_competition_with_no_versions_or_results()
    {
        var competitions = new JsonArray
        {
            new JsonObject
            {
                ["id"] = "regionals-2026",
                ["slug"] = "regionals-2026",
                ["name"] = "Regionals 2026",
                ["status"] = "upcoming",
                ["published_version_id"] = null,
                ["updated_at"] = "2026-06-02T00:00:00Z"
            }
        };

        var rows = CompetitionAdminStore.BuildRows(competitions, new JsonArray(), new JsonArray());

        var row = Assert.Single(rows);
        Assert.Equal("regionals-2026", row.Id);
        Assert.Null(row.ActiveVersion);
        Assert.Equal(0, row.VersionsCount);
        Assert.Null(row.LatestVersion);
        Assert.Null(row.OfficialResultsStatus);
        Assert.Null(row.OfficialResultsImportedAt);
    }

    [Fact]
    public void BuildRows_skips_competitions_missing_required_identity_fields()
    {
        var competitions = new JsonArray
        {
            new JsonObject
            {
                ["id"] = "",
                ["slug"] = "missing-id",
                ["name"] = "Missing Id"
            },
            new JsonObject
            {
                ["id"] = "no-name",
                ["slug"] = "no-name",
                ["name"] = ""
            },
            new JsonObject
            {
                ["id"] = "valid",
                ["slug"] = "valid",
                ["name"] = "Valid",
                ["status"] = "upcoming"
            }
        };

        var rows = CompetitionAdminStore.BuildRows(competitions, new JsonArray(), new JsonArray());

        var row = Assert.Single(rows);
        Assert.Equal("valid", row.Id);
    }

    [Fact]
    public void BuildRows_does_not_cross_join_versions_and_results_across_competitions()
    {
        var competitions = new JsonArray
        {
            new JsonObject
            {
                ["id"] = "a",
                ["slug"] = "a",
                ["name"] = "A",
                ["status"] = "locked",
                ["published_version_id"] = "a-1"
            },
            new JsonObject
            {
                ["id"] = "b",
                ["slug"] = "b",
                ["name"] = "B",
                ["status"] = "upcoming"
            }
        };

        var versions = new JsonArray
        {
            new JsonObject
            {
                ["id"] = "a-1",
                ["competition_id"] = "a",
                ["version"] = "a-v1",
                ["created_at"] = "2026-05-01T00:00:00Z"
            }
        };

        var results = new JsonArray
        {
            new JsonObject
            {
                ["competition_id"] = "a",
                ["status"] = "final",
                ["imported_at"] = "2026-06-01T00:00:00Z"
            }
        };

        var rows = CompetitionAdminStore.BuildRows(competitions, versions, results);

        var a = rows.Single(r => r.Id == "a");
        var b = rows.Single(r => r.Id == "b");

        Assert.Equal("a-v1", a.ActiveVersion);
        Assert.Equal(1, a.VersionsCount);
        Assert.Equal("final", a.OfficialResultsStatus);

        Assert.Null(b.ActiveVersion);
        Assert.Equal(0, b.VersionsCount);
        Assert.Null(b.OfficialResultsStatus);
    }
}
