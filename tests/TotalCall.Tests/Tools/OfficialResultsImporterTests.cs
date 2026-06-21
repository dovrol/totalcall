using TotalCall.Operations.Results;

namespace TotalCall.Tests.Tools;

public sealed class OfficialResultsImporterTests
{
    private readonly OfficialResultsImporter importer = new();

    [Fact]
    public async Task ImportAsync_returns_structured_result_when_credentials_are_missing()
    {
        var result = await importer.ImportAsync(
            new ResultsImportOptions
            {
                CompetitionId = "worlds-2026",
                ResultsJsonPath = LatestWorldsResultsPath()
            },
            CancellationToken.None);

        Assert.Equal(2, result.ExitCode);
        Assert.False(result.Succeeded);
        Assert.Equal("worlds-2026", result.CompetitionId);
        Assert.Equal(OfficialResultImportStatus.Partial, result.Status);
        Assert.True(result.GroupsInFile > 0);
        Assert.False(string.IsNullOrWhiteSpace(result.ResultsHash));
        Assert.Contains(result.Logs, log => log.Level == "error" && log.Message.Contains("SUPABASE_SECRET_KEY"));
    }

    [Fact]
    public async Task ImportAsync_rejects_competition_id_mismatch_before_credentials_check()
    {
        var path = Path.Combine(Path.GetTempPath(), $"totalcall-results-mismatch-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(
            path,
            """
            {
              "competitionId": "other",
              "status": "partial",
              "groups": []
            }
            """,
            CancellationToken.None);

        try
        {
            var result = await importer.ImportAsync(
                new ResultsImportOptions
                {
                    CompetitionId = "worlds-2026",
                    ResultsJsonPath = path
                },
                CancellationToken.None);

            Assert.Equal(1, result.ExitCode);
            Assert.False(result.Succeeded);
            Assert.Contains(result.Logs, log => log.Message.Contains("does not match"));
            Assert.DoesNotContain(result.Logs, log => log.Message.Contains("SUPABASE_SECRET_KEY"));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ImportAsync_reports_missing_file()
    {
        var path = Path.Combine(Path.GetTempPath(), $"totalcall-missing-results-{Guid.NewGuid():N}.json");

        var result = await importer.ImportAsync(
            new ResultsImportOptions
            {
                CompetitionId = "worlds-2026",
                ResultsJsonPath = path
            },
            CancellationToken.None);

        Assert.Equal(1, result.ExitCode);
        Assert.False(result.Succeeded);
        Assert.Contains(result.Logs, log => log.Level == "error" && log.Message.Contains("not found"));
    }

    [Fact]
    public async Task ImportAsync_rejects_invalid_status_before_credentials_check()
    {
        var path = Path.Combine(Path.GetTempPath(), $"totalcall-results-invalid-status-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(
            path,
            """
            {
              "competitionId": "worlds-2026",
              "status": "done",
              "groups": []
            }
            """,
            CancellationToken.None);

        try
        {
            var result = await importer.ImportAsync(
                new ResultsImportOptions
                {
                    CompetitionId = "worlds-2026",
                    ResultsJsonPath = path
                },
                CancellationToken.None);

            Assert.Equal(1, result.ExitCode);
            Assert.False(result.Succeeded);
            Assert.Contains(result.Logs, log => log.Message.Contains("results.status"));
            Assert.DoesNotContain(result.Logs, log => log.Message.Contains("SUPABASE_SECRET_KEY"));
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string LatestWorldsResultsPath() => Path.Combine(
        "ops",
        "data",
        "results",
        "worlds-2026-partial-2026-06-17.json");
}
