using System.Text.Json;
using System.Text.Json.Nodes;
using ModelContextProtocol.Protocol;
using TotalCall.Operations;
using TotalCall.Operations.Competitions;
using TotalCall.Operations.Results;

namespace TotalCall.Mcp;

// Shared state for the MCP tools: the repository root and the operational
// importers from TotalCall.Operations. Registered as a singleton so every tool
// invocation reuses the same resolved root and importer instances.
internal sealed class RepositoryContext
{
    public string RepositoryRoot { get; } = RepositoryPaths.FindRepositoryRoot();

    public CompetitionConfigFileChecker ConfigChecker { get; } = new();

    public OfficialResultsImporter ResultsImporter { get; } = new();

    public CompetitionDefinitionImporter CompetitionImporter { get; } = new();

    public string ResolveInsideRepository(string path) =>
        RepositoryPaths.ResolveInsideRepository(RepositoryRoot, path);

    public string ToRepositoryRelativePath(string path) =>
        RepositoryPaths.ToRepositoryRelativePath(RepositoryRoot, path);

    public static bool HasEnv(string name) =>
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(name));

    public static string? Env(string name) => Environment.GetEnvironmentVariable(name);
}

// Builds CallToolResult payloads that carry both a human-readable text block and
// machine-readable structuredContent, mirroring the previous hand-rolled server.
internal static class ToolResults
{
    private static readonly JsonSerializerOptions Pretty = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public static CallToolResult Structured(JsonNode payload, bool isError = false) => new()
    {
        Content = [new TextContentBlock { Text = payload.ToJsonString(Pretty) }],
        StructuredContent = JsonSerializer.SerializeToElement(payload),
        IsError = isError
    };

    public static CallToolResult Error(string message) => new()
    {
        Content = [new TextContentBlock { Text = message }],
        IsError = true
    };

    public static JsonArray LogsToJson(IEnumerable<OperationLogEntry> logs)
    {
        var json = new JsonArray();
        foreach (var log in logs)
        {
            json.Add(new JsonObject
            {
                ["level"] = log.Level,
                ["message"] = log.Message
            });
        }

        return json;
    }
}

internal static class RepositoryPaths
{
    public static string FindRepositoryRoot()
    {
        var root = FindFrom(Directory.GetCurrentDirectory())
            ?? FindFrom(AppContext.BaseDirectory);

        return root ?? throw new InvalidOperationException("Could not locate TotalCall.sln.");
    }

    public static string ResolveInsideRepository(string repositoryRoot, string path)
    {
        var fullPath = Path.IsPathRooted(path)
            ? Path.GetFullPath(path)
            : Path.GetFullPath(Path.Combine(repositoryRoot, path));
        var normalizedRoot = Path.GetFullPath(repositoryRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        if (!string.Equals(fullPath, normalizedRoot, StringComparison.Ordinal) &&
            !fullPath.StartsWith(normalizedRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal) &&
            !fullPath.StartsWith(normalizedRoot + Path.AltDirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new McpToolArgumentException($"Path must stay inside the repository: {path}");
        }

        return fullPath;
    }

    public static string ToRepositoryRelativePath(string repositoryRoot, string path)
    {
        var fullPath = Path.GetFullPath(path);
        var relative = Path.GetRelativePath(repositoryRoot, fullPath);
        return relative.Replace(Path.DirectorySeparatorChar, '/');
    }

    private static string? FindFrom(string startPath)
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

// Thrown when a tool receives an invalid argument (e.g. a path outside the repo).
// Surfaced to the caller as an error tool result.
internal sealed class McpToolArgumentException(string message) : Exception(message);
