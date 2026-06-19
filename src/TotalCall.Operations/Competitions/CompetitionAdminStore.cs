using System.Text.Json.Nodes;
using TotalCall.Operations.Supabase;

namespace TotalCall.Operations.Competitions;

public sealed record CompetitionAdminOptions
{
    public string? SupabaseUrl { get; init; }
    public string? SupabaseSecretKey { get; init; }
}

public sealed record CompetitionAdminRow(
    string Id,
    string Slug,
    string Name,
    string Status,
    DateTimeOffset? StartDate,
    DateTimeOffset? EndDate,
    DateTimeOffset? PredictionOpenAt,
    DateTimeOffset? PredictionLockAt,
    string? PublishedVersionId,
    string? ActiveVersion,
    int VersionsCount,
    string? LatestVersion,
    DateTimeOffset? LatestVersionCreatedAt,
    string? OfficialResultsStatus,
    DateTimeOffset? OfficialResultsImportedAt,
    string? ScoreSnapshotStatus,
    DateTimeOffset? ScoreSnapshotCalculatedAt);

public sealed record CompetitionAdminVersionRow(
    string Id,
    string CompetitionId,
    string Version,
    DateTimeOffset? PublishedAt,
    DateTimeOffset? CreatedAt,
    bool IsActive);

public sealed record CompetitionAdminDetail(
    CompetitionAdminRow Competition,
    IReadOnlyList<CompetitionAdminVersionRow> Versions);

public sealed class CompetitionAdminStore
{
    public async Task<IReadOnlyList<CompetitionAdminRow>> ListAsync(
        CompetitionAdminOptions options,
        CancellationToken ct)
    {
        var supabase = CreateClient(options);

        var competitionRows = await supabase.GetAsync(
            "public",
            "competitions",
            "select=id,slug,name,status,start_date,end_date,prediction_open_at,prediction_lock_at,published_version_id,updated_at&order=updated_at.desc",
            ct);

        var versionRows = await supabase.GetAsync(
            "public",
            "competition_versions",
            "select=id,competition_id,version,published_at,created_at&order=created_at.desc",
            ct);

        var resultRows = await supabase.GetAsync(
            "public",
            "official_results",
            "select=competition_id,status,imported_at,updated_at&order=updated_at.desc",
            ct);

        return BuildRows(competitionRows, versionRows, resultRows);
    }

    public async Task<CompetitionAdminDetail?> GetDetailAsync(
        CompetitionAdminOptions options,
        string competitionId,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(competitionId))
        {
            return null;
        }

        var rows = await ListAsync(options, ct);
        var competition = rows.FirstOrDefault(row =>
            string.Equals(row.Id, competitionId, StringComparison.OrdinalIgnoreCase));
        if (competition is null)
        {
            return null;
        }

        var supabase = CreateClient(options);
        var versionRows = await supabase.GetAsync(
            "public",
            "competition_versions",
            $"competition_id=eq.{Uri.EscapeDataString(competition.Id)}&select=id,competition_id,version,published_at,created_at&order=created_at.desc",
            ct);

        var versions = versionRows
            .OfType<JsonObject>()
            .Select(row => ParseVersion(row, competition.PublishedVersionId))
            .OfType<CompetitionAdminVersionRow>()
            .ToArray();

        return new CompetitionAdminDetail(competition, versions);
    }

    public static IReadOnlyList<CompetitionAdminRow> BuildRows(
        JsonArray competitionRows,
        JsonArray versionRows,
        JsonArray resultRows)
    {
        var versionsByCompetition = versionRows
            .OfType<JsonObject>()
            .Select(row => ParseVersion(row, activeVersionId: null))
            .OfType<CompetitionAdminVersionRow>()
            .GroupBy(row => row.CompetitionId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.OrdinalIgnoreCase);

        var resultsByCompetition = resultRows
            .OfType<JsonObject>()
            .GroupBy(row => Value(row, "competition_id"), StringComparer.OrdinalIgnoreCase)
            .Where(group => !string.IsNullOrWhiteSpace(group.Key))
            .ToDictionary(group => group.Key!, group => group.First(), StringComparer.OrdinalIgnoreCase);

        var rows = new List<CompetitionAdminRow>();
        foreach (var row in competitionRows.OfType<JsonObject>())
        {
            var id = Value(row, "id");
            var slug = Value(row, "slug");
            var name = Value(row, "name");
            if (string.IsNullOrWhiteSpace(id) ||
                string.IsNullOrWhiteSpace(slug) ||
                string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            var publishedVersionId = Value(row, "published_version_id");
            versionsByCompetition.TryGetValue(id, out var versions);
            versions ??= [];

            var activeVersion = versions.FirstOrDefault(version =>
                string.Equals(version.Id, publishedVersionId, StringComparison.OrdinalIgnoreCase));
            var latestVersion = versions
                .OrderByDescending(version => version.CreatedAt ?? DateTimeOffset.MinValue)
                .FirstOrDefault();

            resultsByCompetition.TryGetValue(id, out var result);

            rows.Add(new CompetitionAdminRow(
                id,
                slug,
                name,
                Value(row, "status") ?? "unknown",
                ParseDate(row, "start_date"),
                ParseDate(row, "end_date"),
                ParseDate(row, "prediction_open_at"),
                ParseDate(row, "prediction_lock_at"),
                publishedVersionId,
                activeVersion?.Version,
                versions.Length,
                latestVersion?.Version,
                latestVersion?.CreatedAt,
                result is null ? null : Value(result, "status"),
                result is null ? null : ParseDate(result, "imported_at"),
                ScoreSnapshotStatus: null,
                ScoreSnapshotCalculatedAt: null));
        }

        return rows;
    }

    private static CompetitionAdminVersionRow? ParseVersion(JsonObject row, string? activeVersionId)
    {
        var id = Value(row, "id");
        var competitionId = Value(row, "competition_id");
        var version = Value(row, "version");
        if (string.IsNullOrWhiteSpace(id) ||
            string.IsNullOrWhiteSpace(competitionId) ||
            string.IsNullOrWhiteSpace(version))
        {
            return null;
        }

        return new CompetitionAdminVersionRow(
            id,
            competitionId,
            version,
            ParseDate(row, "published_at"),
            ParseDate(row, "created_at"),
            string.Equals(id, activeVersionId, StringComparison.OrdinalIgnoreCase));
    }

    private static SupabaseRestClient CreateClient(CompetitionAdminOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.SupabaseUrl) ||
            string.IsNullOrWhiteSpace(options.SupabaseSecretKey))
        {
            throw new InvalidOperationException("SUPABASE_URL and SUPABASE_SECRET_KEY must be set.");
        }

        return new SupabaseRestClient(options.SupabaseUrl, options.SupabaseSecretKey);
    }

    private static string? Value(JsonObject row, string key) => row[key]?.ToString();

    private static DateTimeOffset? ParseDate(JsonObject row, string key)
    {
        var value = Value(row, key);
        return DateTimeOffset.TryParse(value, out var parsed) ? parsed : null;
    }
}
