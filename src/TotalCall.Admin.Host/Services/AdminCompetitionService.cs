using TotalCall.Operations.Admin;
using TotalCall.Operations.Competitions;

namespace TotalCall.Admin.Host.Services;

public sealed record AdminCompetitionGridRow(
    CompetitionAdminRow Competition,
    AdminOperationAuditRecord? LatestOperation);

public sealed record AdminCompetitionDetailView(
    CompetitionAdminDetail Detail,
    IReadOnlyList<AdminOperationAuditRecord> RecentOperations);

public sealed class AdminCompetitionService(
    AdminRuntimeOptions runtimeOptions,
    CompetitionAdminStore competitionStore,
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

    private CompetitionAdminOptions CreateOptions() => new()
    {
        SupabaseUrl = runtimeOptions.SupabaseUrl,
        SupabaseSecretKey = runtimeOptions.SupabaseSecretKey
    };
}
