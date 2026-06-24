using System.ComponentModel;
using System.Text.Json.Nodes;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using TotalCall.Operations.Competitions;
using TotalCall.Operations.Results;

namespace TotalCall.Mcp;

// MCP tools for local TotalCall operations. Read-only/dry-run tools never mutate
// Supabase; write tools require an explicit confirmation argument. Tool names and
// input shapes are kept stable so existing MCP clients keep working.
[McpServerToolType]
internal sealed class TotalCallTools(RepositoryContext repo)
{
    [McpServerTool(Name = "totalcall_runtime_status", Title = "TotalCall runtime status", ReadOnly = true, Idempotent = true)]
    [Description("Returns local runtime status without exposing secret values.")]
    public CallToolResult RuntimeStatus()
    {
        var payload = new JsonObject
        {
            ["repositoryRoot"] = repo.RepositoryRoot,
            ["currentDirectory"] = Directory.GetCurrentDirectory(),
            ["hasSupabaseUrl"] = RepositoryContext.HasEnv("SUPABASE_URL"),
            ["hasSupabaseSecretKey"] = RepositoryContext.HasEnv("SUPABASE_SECRET_KEY"),
            ["hasSupabaseServiceRoleKey"] = RepositoryContext.HasEnv("SUPABASE_SERVICE_ROLE_KEY")
        };

        return ToolResults.Structured(payload);
    }

    [McpServerTool(Name = "totalcall_list_competition_files", Title = "List competition JSON files", ReadOnly = true, Idempotent = true)]
    [Description("Lists local competition config JSON files and their validation status.")]
    public async Task<CallToolResult> ListCompetitionFiles(CancellationToken ct)
    {
        var directory = Path.Combine(repo.RepositoryRoot, "ops", "data", "competitions");
        var files = Directory.Exists(directory)
            ? Directory.GetFiles(directory, "*.json").OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToArray()
            : [];

        var rows = new JsonArray();
        foreach (var file in files)
        {
            var check = await repo.ConfigChecker.CheckAsync(file, ct);
            rows.Add(new JsonObject
            {
                ["path"] = repo.ToRepositoryRelativePath(file),
                ["competitionId"] = check.CompetitionId,
                ["slug"] = check.CompetitionSlug,
                ["name"] = check.CompetitionName,
                ["configVersion"] = check.ConfigVersion,
                ["isValid"] = check.IsValid,
                ["errorsCount"] = check.Errors.Count
            });
        }

        return ToolResults.Structured(new JsonObject
        {
            ["count"] = rows.Count,
            ["files"] = rows
        });
    }

    [McpServerTool(Name = "totalcall_validate_competition_config", Title = "Validate competition config", ReadOnly = true, Idempotent = true)]
    [Description("Validates a repository-local competition JSON file using TotalCall.Operations.")]
    public async Task<CallToolResult> ValidateCompetitionConfig(
        [Description("Repository-relative path to a competition JSON file.")] string competitionJsonPath,
        CancellationToken ct)
    {
        try
        {
            var path = repo.ResolveInsideRepository(competitionJsonPath);
            var check = await repo.ConfigChecker.CheckAsync(path, ct);

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

            var payload = new JsonObject
            {
                ["inputPath"] = repo.ToRepositoryRelativePath(check.ResolvedPath),
                ["fileExists"] = check.FileExists,
                ["parsed"] = check.Parsed,
                ["isValid"] = check.IsValid,
                ["competitionId"] = check.CompetitionId,
                ["slug"] = check.CompetitionSlug,
                ["name"] = check.CompetitionName,
                ["configVersion"] = check.ConfigVersion,
                ["errors"] = errors
            };

            return ToolResults.Structured(payload, isError: !check.IsValid);
        }
        catch (McpToolArgumentException ex)
        {
            return ToolResults.Error(ex.Message);
        }
    }

    [McpServerTool(Name = "totalcall_dry_run_results_import", Title = "Dry-run results import", ReadOnly = true, Idempotent = true)]
    [Description("Validates a repository-local official results JSON file against the published Supabase config without writing data.")]
    public async Task<CallToolResult> DryRunResultsImport(
        [Description("Competition id, for example worlds-2026.")] string competitionId,
        [Description("Repository-relative path to an official results JSON file.")] string resultsJsonPath,
        CancellationToken ct)
    {
        try
        {
            var result = await repo.ResultsImporter.DryRunAsync(
                new ResultsImportOptions
                {
                    CompetitionId = RequireString(competitionId, nameof(competitionId)),
                    ResultsJsonPath = repo.ResolveInsideRepository(resultsJsonPath),
                    SupabaseUrl = RepositoryContext.Env("SUPABASE_URL"),
                    SupabaseSecretKey = RepositoryContext.Env("SUPABASE_SECRET_KEY"),
                    TriggeredBy = "mcp"
                },
                ct);

            return ToolResults.Structured(DryRunToJson(result), isError: !result.IsValid);
        }
        catch (McpToolArgumentException ex)
        {
            return ToolResults.Error(ex.Message);
        }
    }

    [McpServerTool(Name = "totalcall_import_results", Title = "Import official results", ReadOnly = false, Destructive = true, Idempotent = false)]
    [Description("Imports a repository-local official results JSON file into Supabase and recalculates score snapshots. Requires explicit confirmation.")]
    public async Task<CallToolResult> ImportResults(
        [Description("Competition id, for example worlds-2026.")] string competitionId,
        [Description("Repository-relative path to an official results JSON file.")] string resultsJsonPath,
        [Description("Must equal: import <competitionId>.")] string confirmation,
        CancellationToken ct)
    {
        try
        {
            var id = RequireString(competitionId, nameof(competitionId));
            var expected = $"import {id}";
            if (!string.Equals(confirmation, expected, StringComparison.Ordinal))
            {
                return ToolResults.Error($"'confirmation' must be '{expected}'.");
            }

            var result = await repo.ResultsImporter.ImportAsync(
                new ResultsImportOptions
                {
                    CompetitionId = id,
                    ResultsJsonPath = repo.ResolveInsideRepository(resultsJsonPath),
                    SupabaseUrl = RepositoryContext.Env("SUPABASE_URL"),
                    SupabaseSecretKey = RepositoryContext.Env("SUPABASE_SECRET_KEY"),
                    TriggeredBy = "mcp"
                },
                ct);

            return ToolResults.Structured(ImportToJson(result), isError: !result.Succeeded);
        }
        catch (McpToolArgumentException ex)
        {
            return ToolResults.Error(ex.Message);
        }
    }

    [McpServerTool(Name = "totalcall_sync_competition", Title = "Sync competition definition", ReadOnly = false, Destructive = true, Idempotent = false)]
    [Description("Syncs a repository-local competition JSON file to Supabase (metadata + versioned config) and publishes the version. Requires explicit confirmation.")]
    public async Task<CallToolResult> SyncCompetition(
        [Description("Repository-relative path to a competition JSON file, e.g. ops/data/competitions/worlds-2026.json.")] string competitionJsonPath,
        [Description("Must equal: sync <competitionId>, where competitionId comes from the file.")] string confirmation,
        CancellationToken ct)
    {
        try
        {
            var path = repo.ResolveInsideRepository(competitionJsonPath);

            // Derive the competition id from the file so the confirmation phrase names
            // exactly what gets published. Refuse if the id can't be determined.
            var check = await repo.ConfigChecker.CheckAsync(path, ct);
            if (string.IsNullOrWhiteSpace(check.CompetitionId))
            {
                return ToolResults.Error(
                    "Could not determine competitionId from the file. Run totalcall_validate_competition_config first.");
            }

            var expected = $"sync {check.CompetitionId}";
            if (!string.Equals(confirmation, expected, StringComparison.Ordinal))
            {
                return ToolResults.Error($"'confirmation' must be '{expected}'.");
            }

            var result = await repo.CompetitionImporter.SyncAsync(
                new CompetitionSyncOptions
                {
                    CompetitionJsonPath = path,
                    SupabaseUrl = RepositoryContext.Env("SUPABASE_URL"),
                    SupabaseSecretKey = RepositoryContext.Env("SUPABASE_SECRET_KEY"),
                    TriggeredBy = "mcp"
                },
                ct);

            return ToolResults.Structured(SyncToJson(result), isError: !result.Succeeded);
        }
        catch (McpToolArgumentException ex)
        {
            return ToolResults.Error(ex.Message);
        }
    }

    private JsonObject DryRunToJson(ResultsImportDryRunResult result)
    {
        var validationErrors = new JsonArray();
        foreach (var error in result.ValidationErrors)
        {
            validationErrors.Add(error);
        }

        return new JsonObject
        {
            ["exitCode"] = result.ExitCode,
            ["isValid"] = result.IsValid,
            ["competitionId"] = result.CompetitionId,
            ["resultsJsonPath"] = result.ResultsJsonPath is null
                ? null
                : repo.ToRepositoryRelativePath(result.ResultsJsonPath),
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
            ["logs"] = ToolResults.LogsToJson(result.Logs)
        };
    }

    private JsonObject ImportToJson(ResultsImportResult result) => new()
    {
        ["exitCode"] = result.ExitCode,
        ["succeeded"] = result.Succeeded,
        ["competitionId"] = result.CompetitionId,
        ["resultsJsonPath"] = result.ResultsJsonPath is null
            ? null
            : repo.ToRepositoryRelativePath(result.ResultsJsonPath),
        ["status"] = result.Status,
        ["source"] = result.Source,
        ["resultsHash"] = result.ResultsHash,
        ["groupsInFile"] = result.GroupsInFile,
        ["groupsImported"] = result.GroupsImported,
        ["submittedRowsScored"] = result.SubmittedRowsScored,
        ["finalGroupsCount"] = result.FinalGroupsCount,
        ["pendingGroupsCount"] = result.PendingGroupsCount,
        ["leaderboardStatus"] = result.LeaderboardStatus,
        ["calculatedAt"] = result.CalculatedAt?.ToString("O"),
        ["scoreSnapshotsDeleted"] = result.ScoreSnapshotsDeleted,
        ["logs"] = ToolResults.LogsToJson(result.Logs)
    };

    private static JsonObject SyncToJson(CompetitionSyncResult result) => new()
    {
        ["exitCode"] = result.ExitCode,
        ["succeeded"] = result.Succeeded,
        ["competitionId"] = result.CompetitionId,
        ["competitionSlug"] = result.CompetitionSlug,
        ["competitionName"] = result.CompetitionName,
        ["configVersion"] = result.ConfigVersion,
        ["effectiveConfigVersion"] = result.EffectiveConfigVersion,
        ["configHash"] = result.ConfigHash,
        ["publishedVersionId"] = result.PublishedVersionId,
        ["logs"] = ToolResults.LogsToJson(result.Logs)
    };

    private static string RequireString(string? value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new McpToolArgumentException($"'{name}' is required.");
        }

        return value.Trim();
    }
}
