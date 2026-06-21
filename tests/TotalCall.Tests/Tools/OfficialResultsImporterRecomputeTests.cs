using TotalCall.Operations.Results;

namespace TotalCall.Tests.Tools;

public sealed class OfficialResultsImporterRecomputeTests
{
    private readonly OfficialResultsImporter importer = new();

    [Fact]
    public async Task RecomputeScoreSnapshotsAsync_requires_competition_id_before_credentials_check()
    {
        var result = await importer.RecomputeScoreSnapshotsAsync(
            new ScoreSnapshotRecomputeOptions
            {
                CompetitionId = " "
            },
            CancellationToken.None);

        Assert.Equal(1, result.ExitCode);
        Assert.False(result.Succeeded);
        Assert.Null(result.CompetitionId);
        Assert.Contains(result.Logs, log => log.Level == "error" && log.Message.Contains("--competition-id"));
        Assert.DoesNotContain(result.Logs, log => log.Message.Contains("SUPABASE_SECRET_KEY"));
    }

    [Fact]
    public async Task RecomputeScoreSnapshotsAsync_returns_structured_result_when_credentials_are_missing()
    {
        var result = await importer.RecomputeScoreSnapshotsAsync(
            new ScoreSnapshotRecomputeOptions
            {
                CompetitionId = "worlds-2026"
            },
            CancellationToken.None);

        Assert.Equal(2, result.ExitCode);
        Assert.False(result.Succeeded);
        Assert.Equal("worlds-2026", result.CompetitionId);
        Assert.Equal(0, result.OfficialResultGroupsCount);
        Assert.Equal(0, result.SubmittedRowsScored);
        Assert.Contains(result.Logs, log => log.Level == "error" && log.Message.Contains("SUPABASE_SECRET_KEY"));
    }
}
