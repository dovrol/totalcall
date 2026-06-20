using TotalCall.Operations.Results;

namespace TotalCall.Tests.Tools;

public sealed class OfficialResultsImporterDryRunTests
{
    private readonly OfficialResultsImporter importer = new();

    [Fact]
    public async Task DryRunAsync_previews_file_then_stops_at_missing_credentials()
    {
        var result = await importer.DryRunAsync(
            new ResultsImportOptions
            {
                CompetitionId = "worlds-2026",
                ResultsJsonPath = LatestWorldsResultsPath()
            },
            CancellationToken.None);

        // Config-aware validation needs the published config, so a dry run still
        // requires credentials — but the file-derived preview is already populated.
        Assert.Equal(2, result.ExitCode);
        Assert.False(result.IsValid);
        Assert.Equal("worlds-2026", result.CompetitionId);
        Assert.Equal(OfficialResultImportStatus.Partial, result.Status);
        Assert.True(result.GroupsInFile > 0);
        Assert.Equal(result.GroupsInFile, result.FinalGroupsInFile + result.PendingGroupsInFile);
        Assert.False(string.IsNullOrWhiteSpace(result.ResultsHash));
        Assert.False(result.CompetitionPublished);
        Assert.Contains(result.Logs, log => log.Level == "error" && log.Message.Contains("SUPABASE_SECRET_KEY"));
    }

    [Fact]
    public async Task DryRunAsync_reports_competition_id_mismatch()
    {
        await WithTempResults(
            """
            {
              "competitionId": "other",
              "status": "partial",
              "groups": []
            }
            """,
            async path =>
            {
                var result = await importer.DryRunAsync(
                    new ResultsImportOptions { CompetitionId = "worlds-2026", ResultsJsonPath = path },
                    CancellationToken.None);

                Assert.Equal(1, result.ExitCode);
                Assert.False(result.IsValid);
                Assert.Contains(result.Logs, log => log.Message.Contains("does not match"));
                Assert.DoesNotContain(result.Logs, log => log.Message.Contains("SUPABASE_SECRET_KEY"));
            });
    }

    [Fact]
    public async Task DryRunAsync_reports_missing_file()
    {
        var path = Path.Combine(Path.GetTempPath(), $"totalcall-dryrun-missing-{Guid.NewGuid():N}.json");

        var result = await importer.DryRunAsync(
            new ResultsImportOptions { CompetitionId = "worlds-2026", ResultsJsonPath = path },
            CancellationToken.None);

        Assert.Equal(1, result.ExitCode);
        Assert.False(result.IsValid);
        Assert.Contains(result.Logs, log => log.Level == "error" && log.Message.Contains("not found"));
    }

    [Fact]
    public async Task DryRunAsync_surfaces_shape_validation_errors()
    {
        await WithTempResults(
            """
            {
              "competitionId": "worlds-2026",
              "status": "done",
              "groups": []
            }
            """,
            async path =>
            {
                var result = await importer.DryRunAsync(
                    new ResultsImportOptions { CompetitionId = "worlds-2026", ResultsJsonPath = path },
                    CancellationToken.None);

                Assert.Equal(1, result.ExitCode);
                Assert.False(result.IsValid);
                Assert.Contains(result.ValidationErrors, error => error.Contains("results.status"));
                Assert.DoesNotContain(result.Logs, log => log.Message.Contains("SUPABASE_SECRET_KEY"));
            });
    }

    private static async Task WithTempResults(string json, Func<string, Task> body)
    {
        var path = Path.Combine(Path.GetTempPath(), $"totalcall-dryrun-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(path, json, CancellationToken.None);
        try
        {
            await body(path);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string LatestWorldsResultsPath() => Path.Combine(
        "tools",
        "sync",
        "data",
        "results",
        "worlds-2026-partial-2026-06-17.json");
}
