using TotalCall.Operations.Competitions;

namespace TotalCall.Tests.Tools;

public sealed class CompetitionDefinitionImporterTests
{
    private readonly CompetitionDefinitionImporter importer = new();

    [Fact]
    public async Task SyncAsync_returns_structured_result_when_credentials_are_missing()
    {
        var result = await importer.SyncAsync(
            new CompetitionSyncOptions
            {
                CompetitionJsonPath = WorldsConfigPath()
            },
            CancellationToken.None);

        Assert.Equal(2, result.ExitCode);
        Assert.False(result.Succeeded);
        Assert.Equal("worlds-2026", result.CompetitionId);
        Assert.Equal("2026.4", result.ConfigVersion);
        Assert.False(string.IsNullOrWhiteSpace(result.ConfigHash));
        Assert.Contains(result.Logs, log => log.Level == "error" && log.Message.Contains("SUPABASE_SECRET_KEY"));
    }

    [Fact]
    public async Task SyncAsync_reports_validation_errors_before_credentials_check()
    {
        var path = Path.Combine(Path.GetTempPath(), $"totalcall-invalid-competition-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(
            path,
            """
            {
              "id": "bad",
              "slug": "bad",
              "name": "Bad Competition",
              "configVersion": "v1",
              "athletes": [],
              "categories": [],
              "predictionGroups": []
            }
            """,
            CancellationToken.None);

        try
        {
            var result = await importer.SyncAsync(
                new CompetitionSyncOptions
                {
                    CompetitionJsonPath = path
                },
                CancellationToken.None);

            Assert.Equal(1, result.ExitCode);
            Assert.False(result.Succeeded);
            Assert.Contains(result.Logs, log => log.Message.Contains("Competition config validation failed"));
            Assert.DoesNotContain(result.Logs, log => log.Message.Contains("SUPABASE_SECRET_KEY"));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task SyncAsync_reports_missing_file()
    {
        var path = Path.Combine(Path.GetTempPath(), $"totalcall-missing-competition-{Guid.NewGuid():N}.json");

        var result = await importer.SyncAsync(
            new CompetitionSyncOptions
            {
                CompetitionJsonPath = path
            },
            CancellationToken.None);

        Assert.Equal(1, result.ExitCode);
        Assert.False(result.Succeeded);
        Assert.Contains(result.Logs, log => log.Level == "error" && log.Message.Contains("not found"));
    }

    private static string WorldsConfigPath() => Path.Combine(
        FindRepositoryRoot(),
        "src",
        "TotalCall.Client",
        "wwwroot",
        "data",
        "competitions",
        "worlds-2026.json");

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "TotalCall.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("Could not find repository root.");
    }
}
