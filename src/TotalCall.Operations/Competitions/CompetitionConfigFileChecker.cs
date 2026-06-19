using System.Text.Json;
using TotalCall.Core.Domain.Competitions;
using TotalCall.Core.Validation;

namespace TotalCall.Operations.Competitions;

public sealed class CompetitionConfigFileChecker
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly CompetitionConfigValidator validator = new();

    public async Task<CompetitionConfigFileCheckResult> CheckAsync(
        string? competitionJsonPath,
        CancellationToken ct)
    {
        var inputPath = competitionJsonPath?.Trim();
        if (string.IsNullOrWhiteSpace(inputPath))
        {
            return Failure(
                string.Empty,
                string.Empty,
                "path",
                "CompetitionJsonPathRequired",
                "Competition JSON path is required.");
        }

        var resolvedPath = ResolveFullPath(inputPath);
        if (!File.Exists(resolvedPath))
        {
            return Failure(
                inputPath,
                resolvedPath,
                "path",
                "CompetitionJsonNotFound",
                $"Competition JSON was not found at '{resolvedPath}'.");
        }

        string rawJson;
        try
        {
            rawJson = await File.ReadAllTextAsync(resolvedPath, ct);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Failure(
                inputPath,
                resolvedPath,
                "path",
                "CompetitionJsonUnreadable",
                $"Competition JSON could not be read: {ex.Message}");
        }

        Competition? competition;
        try
        {
            competition = JsonSerializer.Deserialize<Competition>(rawJson, JsonOptions);
        }
        catch (JsonException ex)
        {
            return Failure(
                inputPath,
                resolvedPath,
                "json",
                "CompetitionJsonInvalid",
                $"Competition JSON could not be parsed: {ex.Message}");
        }

        if (competition is null)
        {
            return Failure(
                inputPath,
                resolvedPath,
                "json",
                "CompetitionJsonEmpty",
                "Competition JSON did not contain a competition object.");
        }

        var validation = validator.Validate(competition);
        var errors = validation.Errors
            .Select(error => new CompetitionConfigFileCheckError(error.Path, error.Code, error.Message))
            .ToArray();

        return new CompetitionConfigFileCheckResult(
            inputPath,
            resolvedPath,
            FileExists: true,
            Parsed: true,
            validation.IsValid,
            competition.Id,
            competition.Slug,
            competition.Name,
            competition.ConfigVersion,
            errors);
    }

    private static CompetitionConfigFileCheckResult Failure(
        string inputPath,
        string resolvedPath,
        string path,
        string code,
        string message) => new(
            inputPath,
            resolvedPath,
            FileExists: false,
            Parsed: false,
            IsValid: false,
            CompetitionId: null,
            CompetitionSlug: null,
            CompetitionName: null,
            ConfigVersion: null,
            Errors: [new CompetitionConfigFileCheckError(path, code, message)]);

    private static string ResolveFullPath(string path)
    {
        if (Path.IsPathRooted(path))
        {
            return Path.GetFullPath(path);
        }

        var currentDirectoryPath = Path.GetFullPath(path);
        if (File.Exists(currentDirectoryPath))
        {
            return currentDirectoryPath;
        }

        var repositoryRoot = FindRepositoryRoot(Directory.GetCurrentDirectory())
            ?? FindRepositoryRoot(AppContext.BaseDirectory);
        if (repositoryRoot is not null)
        {
            var repositoryPath = Path.GetFullPath(Path.Combine(repositoryRoot, path));
            if (File.Exists(repositoryPath) || IsRepositoryRelativePath(path))
            {
                return repositoryPath;
            }
        }

        return currentDirectoryPath;
    }

    private static bool IsRepositoryRelativePath(string path) =>
        path.StartsWith("src/", StringComparison.Ordinal)
        || path.StartsWith("src\\", StringComparison.Ordinal)
        || path.StartsWith("tools/", StringComparison.Ordinal)
        || path.StartsWith("tools\\", StringComparison.Ordinal)
        || path.StartsWith("supabase/", StringComparison.Ordinal)
        || path.StartsWith("supabase\\", StringComparison.Ordinal);

    private static string? FindRepositoryRoot(string startPath)
    {
        var directory = new DirectoryInfo(startPath);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "TotalCall.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return null;
    }
}

public sealed record CompetitionConfigFileCheckResult(
    string InputPath,
    string ResolvedPath,
    bool FileExists,
    bool Parsed,
    bool IsValid,
    string? CompetitionId,
    string? CompetitionSlug,
    string? CompetitionName,
    string? ConfigVersion,
    IReadOnlyList<CompetitionConfigFileCheckError> Errors);

public sealed record CompetitionConfigFileCheckError(
    string Path,
    string Code,
    string Message);
