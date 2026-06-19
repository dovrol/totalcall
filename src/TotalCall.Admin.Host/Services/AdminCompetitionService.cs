using System.Text.Json.Nodes;
using TotalCall.Operations.Admin;
using TotalCall.Operations.Competitions;

namespace TotalCall.Admin.Host.Services;

public sealed record AdminCompetitionGridRow(
    CompetitionAdminRow Competition,
    AdminOperationAuditRecord? LatestOperation);

public sealed record AdminCompetitionDetailView(
    CompetitionAdminDetail Detail,
    IReadOnlyList<AdminOperationAuditRecord> RecentOperations);

public sealed record AdminCompetitionConfigDiffView(
    string LocalPath,
    bool LocalAvailable,
    string? LocalConfigVersion,
    string? LocalError,
    bool HasActiveVersion,
    string? ActiveVersion,
    ConfigDiffResult? Diff);

public sealed class AdminCompetitionService(
    AdminRuntimeOptions runtimeOptions,
    CompetitionAdminStore competitionStore,
    CompetitionConfigFileChecker configChecker,
    AdminOperationAuditService auditService)
{
    public async Task<IReadOnlyList<AdminCompetitionGridRow>> ListAsync(CancellationToken ct)
    {
        var competitions = await competitionStore.ListAsync(CreateOptions(), ct);
        var recentOperations = await auditService.ListRecentAsync(100, ct);
        var latestByCompetition = recentOperations
            .Where(record => string.Equals(record.TargetType, "competition", StringComparison.OrdinalIgnoreCase) &&
                             !string.IsNullOrWhiteSpace(record.TargetId))
            .GroupBy(record => record.TargetId!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        return competitions
            .Select(competition =>
            {
                latestByCompetition.TryGetValue(competition.Id, out var latestOperation);
                return new AdminCompetitionGridRow(competition, latestOperation);
            })
            .ToArray();
    }

    public async Task<AdminCompetitionDetailView?> GetDetailAsync(string competitionId, CancellationToken ct)
    {
        var detail = await competitionStore.GetDetailAsync(CreateOptions(), competitionId, ct);
        if (detail is null)
        {
            return null;
        }

        var recentOperations = await auditService.ListRecentAsync(100, ct);
        var scopedOperations = recentOperations
            .Where(record => string.Equals(record.TargetType, "competition", StringComparison.OrdinalIgnoreCase) &&
                             string.Equals(record.TargetId, detail.Competition.Id, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        return new AdminCompetitionDetailView(detail, scopedOperations);
    }

    // Compares the conventional local competition JSON against the config the
    // competition is currently published at, so an admin can see drift before
    // publishing. Local-only concern: the local JSON is a dev/import source.
    public async Task<AdminCompetitionConfigDiffView> GetConfigDiffAsync(
        string competitionId,
        string slug,
        CancellationToken ct)
    {
        var localPath = $"src/TotalCall.Client/wwwroot/data/competitions/{slug}.json";
        var active = await competitionStore.GetActiveConfigAsync(CreateOptions(), competitionId, ct);
        var hasActiveVersion = active is not null;

        var check = await configChecker.CheckAsync(localPath, ct);
        if (!check.Parsed)
        {
            var message = check.Errors.Count > 0
                ? check.Errors[0].Message
                : "Local competition JSON could not be read.";
            return new AdminCompetitionConfigDiffView(
                localPath, LocalAvailable: false, LocalConfigVersion: null, LocalError: message,
                hasActiveVersion, active?.Version, Diff: null);
        }

        JsonNode? localConfig;
        try
        {
            localConfig = JsonNode.Parse(await File.ReadAllTextAsync(check.ResolvedPath, ct));
        }
        catch (Exception ex) when (ex is IOException or System.Text.Json.JsonException)
        {
            return new AdminCompetitionConfigDiffView(
                localPath, LocalAvailable: false, LocalConfigVersion: check.ConfigVersion,
                LocalError: $"Local competition JSON could not be read: {ex.Message}",
                hasActiveVersion, active?.Version, Diff: null);
        }

        var diff = active is null
            ? null
            : CompetitionConfigDiff.Compare(localConfig, active.Config);

        return new AdminCompetitionConfigDiffView(
            localPath, LocalAvailable: true, check.ConfigVersion, LocalError: null,
            hasActiveVersion, active?.Version, diff);
    }

    private CompetitionAdminOptions CreateOptions() => new()
    {
        SupabaseUrl = runtimeOptions.SupabaseUrl,
        SupabaseSecretKey = runtimeOptions.SupabaseSecretKey
    };
}
