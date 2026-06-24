using TotalCall.Operations.Competitions;

namespace TotalCall.Tests.Tools;

public sealed class CompetitionConfigFileCheckerTests
{
    private readonly CompetitionConfigFileChecker checker = new();

    [Fact]
    public async Task CheckAsync_accepts_current_worlds_config()
    {
        var path = Path.Combine(
            FindRepositoryRoot(),
            "ops",
            "data",
            "competitions",
            "worlds-2026.json");

        var result = await checker.CheckAsync(path, CancellationToken.None);

        Assert.True(
            result.IsValid,
            string.Join(Environment.NewLine, result.Errors.Select(error => $"{error.Path}: {error.Message}")));
        Assert.True(result.FileExists);
        Assert.True(result.Parsed);
        Assert.Equal("worlds-2026", result.CompetitionId);
        Assert.Equal("Worlds 2026", result.CompetitionName);
    }

    [Fact]
    public async Task CheckAsync_reports_invalid_json()
    {
        var path = Path.Combine(Path.GetTempPath(), $"totalcall-config-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(path, "{", CancellationToken.None);

        try
        {
            var result = await checker.CheckAsync(path, CancellationToken.None);

            Assert.False(result.IsValid);
            Assert.False(result.Parsed);
            Assert.Contains(result.Errors, error => error.Code == "CompetitionJsonInvalid");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task CheckAsync_reports_missing_file()
    {
        var path = Path.Combine(Path.GetTempPath(), $"totalcall-missing-{Guid.NewGuid():N}.json");

        var result = await checker.CheckAsync(path, CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.False(result.FileExists);
        Assert.Contains(result.Errors, error => error.Code == "CompetitionJsonNotFound");
    }

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
