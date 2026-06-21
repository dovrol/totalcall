using System.Text.Json;
using System.Text.Json.Nodes;
using TotalCall.Operations.Competitions;
using TotalCall.Operations.Results;

await new TotalCallMcpServer(Console.In, Console.Out).RunAsync(CancellationToken.None);

internal sealed class TotalCallMcpServer(TextReader input, TextWriter output)
{
    private const string ProtocolVersion = "2025-06-18";

    private static readonly JsonSerializerOptions CompactJson = new(JsonSerializerDefaults.Web);
    private static readonly JsonSerializerOptions PrettyJson = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly string repositoryRoot = RepositoryPaths.FindRepositoryRoot();
    private readonly CompetitionConfigFileChecker configChecker = new();
    private readonly OfficialResultsImporter resultsImporter = new();

    public async Task RunAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var line = await input.ReadLineAsync(ct);
            if (line is null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            await HandleLineAsync(line, ct);
        }
    }

    private async Task HandleLineAsync(string line, CancellationToken ct)
    {
        JsonObject? request;
        try
        {
            request = JsonNode.Parse(line) as JsonObject;
        }
        catch (JsonException ex)
        {
            Console.Error.WriteLine($"Invalid MCP JSON message: {ex.Message}");
            return;
        }

        if (request is null)
        {
            Console.Error.WriteLine("Invalid MCP JSON message: root must be an object.");
            return;
        }

        var id = request["id"]?.DeepClone();
        var method = request["method"]?.ToString();
        var isRequest = id is not null;

        if (string.IsNullOrWhiteSpace(method))
        {
            if (isRequest)
            {
                await WriteErrorAsync(id, -32600, "Missing JSON-RPC method.", ct);
            }

            return;
        }

        try
        {
            switch (method)
            {
                case "initialize":
                    await WriteResultAsync(id, InitializeResult(), ct);
                    return;
                case "notifications/initialized":
                    return;
                case "ping":
                    if (isRequest)
                    {
                        await WriteResultAsync(id, new JsonObject(), ct);
                    }

                    return;
                case "tools/list":
                    await WriteResultAsync(id, new JsonObject { ["tools"] = ToolDefinitions.List() }, ct);
                    return;
                case "tools/call":
                    await WriteResultAsync(id, await CallToolAsync(request["params"] as JsonObject, ct), ct);
                    return;
                default:
                    if (isRequest)
                    {
                        await WriteErrorAsync(id, -32601, $"Unknown method: {method}", ct);
                    }

                    return;
            }
        }
        catch (ToolArgumentException ex)
        {
            await WriteResultAsync(id, ToolResult.Error(ex.Message), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Console.Error.WriteLine($"MCP tool failure: {ex}");
            await WriteResultAsync(id, ToolResult.Error(ex.Message), ct);
        }
    }

    private JsonObject InitializeResult() => new()
    {
        ["protocolVersion"] = ProtocolVersion,
        ["capabilities"] = new JsonObject
        {
            ["tools"] = new JsonObject
            {
                ["listChanged"] = false
            }
        },
        ["serverInfo"] = new JsonObject
        {
            ["name"] = "totalcall-ops",
            ["title"] = "TotalCall Operations",
            ["version"] = "0.1.0"
        },
        ["instructions"] = "Use these tools for local TotalCall operations. Current tools are read-only or dry-run only; they do not mutate Supabase."
    };

    private async Task<JsonObject> CallToolAsync(JsonObject? parameters, CancellationToken ct)
    {
        if (parameters is null)
        {
            throw new ToolArgumentException("tools/call params are required.");
        }

        var name = parameters["name"]?.ToString();
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ToolArgumentException("tools/call params.name is required.");
        }

        var arguments = parameters["arguments"] as JsonObject ?? new JsonObject();
        return name switch
        {
            "totalcall_runtime_status" => ToolResult.Success(RuntimeStatus()),
            "totalcall_list_competition_files" => await ListCompetitionFilesAsync(ct),
            "totalcall_validate_competition_config" => await ValidateCompetitionConfigAsync(arguments, ct),
            "totalcall_dry_run_results_import" => await DryRunResultsImportAsync(arguments, ct),
            _ => ToolResult.Error($"Unknown tool: {name}")
        };
    }

    private JsonObject RuntimeStatus() => new()
    {
        ["repositoryRoot"] = repositoryRoot,
        ["currentDirectory"] = Directory.GetCurrentDirectory(),
        ["hasSupabaseUrl"] = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SUPABASE_URL")),
        ["hasSupabaseSecretKey"] = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SUPABASE_SECRET_KEY")),
        ["hasSupabaseServiceRoleKey"] = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SUPABASE_SERVICE_ROLE_KEY"))
    };

    private async Task<JsonObject> ListCompetitionFilesAsync(CancellationToken ct)
    {
        var directory = Path.Combine(repositoryRoot, "src", "TotalCall.Client", "wwwroot", "data", "competitions");
        var files = Directory.Exists(directory)
            ? Directory.GetFiles(directory, "*.json").OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToArray()
            : [];

        var rows = new JsonArray();
        foreach (var file in files)
        {
            var check = await configChecker.CheckAsync(file, ct);
            rows.Add(new JsonObject
            {
                ["path"] = RepositoryPaths.ToRepositoryRelativePath(repositoryRoot, file),
                ["competitionId"] = check.CompetitionId,
                ["slug"] = check.CompetitionSlug,
                ["name"] = check.CompetitionName,
                ["configVersion"] = check.ConfigVersion,
                ["isValid"] = check.IsValid,
                ["errorsCount"] = check.Errors.Count
            });
        }

        var result = new JsonObject
        {
            ["count"] = rows.Count,
            ["files"] = rows
        };

        return ToolResult.Success(result);
    }

    private async Task<JsonObject> ValidateCompetitionConfigAsync(JsonObject arguments, CancellationToken ct)
    {
        var path = RequireRepositoryPath(arguments, "competitionJsonPath");
        var check = await configChecker.CheckAsync(path, ct);

        var errors = new JsonArray();
        foreach (var error in check.Errors)
        {
            errors.Add(new JsonObject
            {
                ["path"] = error.Path,
                ["code"] = error.Code,
                ["message"] = error.Message
            });
        }

        var result = new JsonObject
        {
            ["inputPath"] = RepositoryPaths.ToRepositoryRelativePath(repositoryRoot, check.ResolvedPath),
            ["fileExists"] = check.FileExists,
            ["parsed"] = check.Parsed,
            ["isValid"] = check.IsValid,
            ["competitionId"] = check.CompetitionId,
            ["slug"] = check.CompetitionSlug,
            ["name"] = check.CompetitionName,
            ["configVersion"] = check.ConfigVersion,
            ["errors"] = errors
        };

        return ToolResult.Success(result, isError: !check.IsValid);
    }

    private async Task<JsonObject> DryRunResultsImportAsync(JsonObject arguments, CancellationToken ct)
    {
        var competitionId = RequiredString(arguments, "competitionId");
        var resultsJsonPath = RequireRepositoryPath(arguments, "resultsJsonPath");

        var result = await resultsImporter.DryRunAsync(
            new ResultsImportOptions
            {
                CompetitionId = competitionId,
                ResultsJsonPath = resultsJsonPath,
                SupabaseUrl = Environment.GetEnvironmentVariable("SUPABASE_URL"),
                SupabaseSecretKey = Environment.GetEnvironmentVariable("SUPABASE_SECRET_KEY"),
                TriggeredBy = "mcp"
            },
            ct);

        return ToolResult.Success(ResultsDryRunToJson(result), isError: !result.IsValid);
    }

    private JsonObject ResultsDryRunToJson(ResultsImportDryRunResult result)
    {
        var validationErrors = new JsonArray();
        foreach (var error in result.ValidationErrors)
        {
            validationErrors.Add(error);
        }

        var logs = new JsonArray();
        foreach (var log in result.Logs)
        {
            logs.Add(new JsonObject
            {
                ["level"] = log.Level,
                ["message"] = log.Message
            });
        }

        return new JsonObject
        {
            ["exitCode"] = result.ExitCode,
            ["isValid"] = result.IsValid,
            ["competitionId"] = result.CompetitionId,
            ["resultsJsonPath"] = result.ResultsJsonPath is null
                ? null
                : RepositoryPaths.ToRepositoryRelativePath(repositoryRoot, result.ResultsJsonPath),
            ["status"] = result.Status,
            ["source"] = result.Source,
            ["resultsHash"] = result.ResultsHash,
            ["groupsInFile"] = result.GroupsInFile,
            ["finalGroupsInFile"] = result.FinalGroupsInFile,
            ["pendingGroupsInFile"] = result.PendingGroupsInFile,
            ["distinctAthletesReferenced"] = result.DistinctAthletesReferenced,
            ["competitionPublished"] = result.CompetitionPublished,
            ["activeConfigVersion"] = result.ActiveConfigVersion,
            ["storedResultsHash"] = result.StoredResultsHash,
            ["matchesStoredResults"] = result.MatchesStoredResults,
            ["validationErrors"] = validationErrors,
            ["logs"] = logs
        };
    }

    private string RequireRepositoryPath(JsonObject arguments, string key)
    {
        var value = RequiredString(arguments, key);
        return RepositoryPaths.ResolveInsideRepository(repositoryRoot, value);
    }

    private static string RequiredString(JsonObject arguments, string key)
    {
        var value = arguments[key]?.ToString();
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ToolArgumentException($"'{key}' is required.");
        }

        return value.Trim();
    }

    private async Task WriteResultAsync(JsonNode? id, JsonObject result, CancellationToken ct)
    {
        if (id is null)
        {
            return;
        }

        await WriteMessageAsync(
            new JsonObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = id,
                ["result"] = result
            },
            ct);
    }

    private async Task WriteErrorAsync(JsonNode? id, int code, string message, CancellationToken ct)
    {
        if (id is null)
        {
            return;
        }

        await WriteMessageAsync(
            new JsonObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = id,
                ["error"] = new JsonObject
                {
                    ["code"] = code,
                    ["message"] = message
                }
            },
            ct);
    }

    private async Task WriteMessageAsync(JsonObject message, CancellationToken ct)
    {
        await output.WriteLineAsync(message.ToJsonString(CompactJson).AsMemory(), ct);
        await output.FlushAsync(ct);
    }

    private static class ToolResult
    {
        public static JsonObject Success(JsonObject structuredContent, bool isError = false) => new()
        {
            ["content"] = new JsonArray
            {
                new JsonObject
                {
                    ["type"] = "text",
                    ["text"] = structuredContent.ToJsonString(PrettyJson)
                }
            },
            ["structuredContent"] = structuredContent.DeepClone(),
            ["isError"] = isError
        };

        public static JsonObject Error(string message) => new()
        {
            ["content"] = new JsonArray
            {
                new JsonObject
                {
                    ["type"] = "text",
                    ["text"] = message
                }
            },
            ["isError"] = true
        };
    }
}

internal static class ToolDefinitions
{
    public static JsonArray List() =>
    [
        Tool(
            "totalcall_runtime_status",
            "TotalCall runtime status",
            "Returns local runtime status without exposing secret values.",
            new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject(),
                ["additionalProperties"] = false
            }),
        Tool(
            "totalcall_list_competition_files",
            "List competition JSON files",
            "Lists local competition config JSON files and their validation status.",
            new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject(),
                ["additionalProperties"] = false
            }),
        Tool(
            "totalcall_validate_competition_config",
            "Validate competition config",
            "Validates a repository-local competition JSON file using TotalCall.Operations.",
            new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject
                {
                    ["competitionJsonPath"] = new JsonObject
                    {
                        ["type"] = "string",
                        ["description"] = "Repository-relative path to a competition JSON file."
                    }
                },
                ["required"] = new JsonArray("competitionJsonPath"),
                ["additionalProperties"] = false
            }),
        Tool(
            "totalcall_dry_run_results_import",
            "Dry-run results import",
            "Validates a repository-local official results JSON file against the published Supabase config without writing data.",
            new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject
                {
                    ["competitionId"] = new JsonObject
                    {
                        ["type"] = "string",
                        ["description"] = "Competition id, for example worlds-2026."
                    },
                    ["resultsJsonPath"] = new JsonObject
                    {
                        ["type"] = "string",
                        ["description"] = "Repository-relative path to an official results JSON file."
                    }
                },
                ["required"] = new JsonArray("competitionId", "resultsJsonPath"),
                ["additionalProperties"] = false
            })
    ];

    private static JsonObject Tool(string name, string title, string description, JsonObject inputSchema) => new()
    {
        ["name"] = name,
        ["title"] = title,
        ["description"] = description,
        ["inputSchema"] = inputSchema,
        ["annotations"] = new JsonObject
        {
            ["readOnlyHint"] = true
        }
    };
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
            throw new ToolArgumentException($"Path must stay inside the repository: {path}");
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

internal sealed class ToolArgumentException(string message) : Exception(message);
