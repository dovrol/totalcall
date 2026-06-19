using System.Text.Json.Nodes;
using TotalCall.Operations;
using TotalCall.Operations.Admin;
using TotalCall.Operations.Competitions;
using TotalCall.Operations.Results;

namespace TotalCall.Admin.Host.Services;

public sealed class AdminOperationAuditService(
    AdminRuntimeOptions runtimeOptions,
    AdminOperationAuditStore auditStore)
{
    public Task<IReadOnlyList<AdminOperationAuditRecord>> ListRecentAsync(
        int limit,
        CancellationToken ct) =>
        auditStore.ListRecentAsync(CreateOptions(), limit, ct);

    public Task<string> RecordCompetitionPublishAsync(
        CompetitionConfigFileCheckResult config,
        CompetitionSyncResult result,
        DateTimeOffset startedAt,
        DateTimeOffset finishedAt,
        CancellationToken ct)
    {
        var input = new JsonObject
        {
            ["competition_id"] = config.CompetitionId,
            ["config_version"] = config.ConfigVersion,
            ["file_name"] = Path.GetFileName(config.ResolvedPath)
        };
        var output = new JsonObject
        {
            ["exit_code"] = result.ExitCode,
            ["config_version"] = result.ConfigVersion,
            ["effective_config_version"] = result.EffectiveConfigVersion,
            ["published_version_id"] = result.PublishedVersionId,
            ["config_hash"] = result.ConfigHash
        };

        return RecordAsync(
            AdminOperationType.CompetitionConfigPublish,
            result.Succeeded ? AdminOperationStatus.Succeeded : AdminOperationStatus.Failed,
            "competition",
            config.CompetitionId,
            startedAt,
            finishedAt,
            input,
            output,
            result.Logs,
            FirstError(result.Logs),
            ct);
    }

    public Task<string> RecordResultsImportAsync(
        string competitionId,
        string resultsJsonPath,
        ResultsImportResult result,
        DateTimeOffset startedAt,
        DateTimeOffset finishedAt,
        CancellationToken ct)
    {
        var input = new JsonObject
        {
            ["competition_id"] = competitionId,
            ["file_name"] = Path.GetFileName(result.ResultsJsonPath ?? resultsJsonPath)
        };
        var output = new JsonObject
        {
            ["exit_code"] = result.ExitCode,
            ["status"] = result.Status,
            ["source"] = result.Source,
            ["results_hash"] = result.ResultsHash,
            ["groups_in_file"] = result.GroupsInFile,
            ["groups_imported"] = result.GroupsImported,
            ["submitted_rows_scored"] = result.SubmittedRowsScored,
            ["final_groups_count"] = result.FinalGroupsCount,
            ["pending_groups_count"] = result.PendingGroupsCount,
            ["leaderboard_status"] = result.LeaderboardStatus,
            ["calculated_at"] = result.CalculatedAt?.ToString("o"),
            ["score_snapshots_deleted"] = result.ScoreSnapshotsDeleted
        };

        return RecordAsync(
            AdminOperationType.OfficialResultsImport,
            result.Succeeded ? AdminOperationStatus.Succeeded : AdminOperationStatus.Failed,
            "competition",
            competitionId,
            startedAt,
            finishedAt,
            input,
            output,
            result.Logs,
            FirstError(result.Logs),
            ct);
    }

    private Task<string> RecordAsync(
        string operationType,
        string status,
        string targetType,
        string? targetId,
        DateTimeOffset startedAt,
        DateTimeOffset finishedAt,
        JsonObject input,
        JsonObject output,
        IReadOnlyList<OperationLogEntry> logs,
        string? errorMessage,
        CancellationToken ct)
    {
        var record = new AdminOperationAuditRecord(
            Id: null,
            operationType,
            status,
            targetType,
            targetId,
            startedAt,
            finishedAt,
            TriggeredBy: "admin-host",
            RuntimeOrigin: runtimeOptions.SupabaseOrigin,
            input,
            output,
            logs,
            errorMessage);

        return auditStore.RecordAsync(CreateOptions(), record, ct);
    }

    private AdminOperationAuditOptions CreateOptions() => new()
    {
        SupabaseUrl = runtimeOptions.SupabaseUrl,
        SupabaseSecretKey = runtimeOptions.SupabaseSecretKey
    };

    private static string? FirstError(IReadOnlyList<OperationLogEntry> logs) =>
        logs.FirstOrDefault(log => string.Equals(log.Level, "error", StringComparison.OrdinalIgnoreCase))?.Message;
}
