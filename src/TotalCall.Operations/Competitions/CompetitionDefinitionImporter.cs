using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using TotalCall.Core.Domain.Competitions;
using TotalCall.Core.Validation;
using TotalCall.Operations;
using TotalCall.Operations.Supabase;

namespace TotalCall.Operations.Competitions;

public sealed class CompetitionSyncOptions
{
    public required string CompetitionJsonPath { get; init; }
    public string? SupabaseUrl { get; init; }
    public string? SupabaseSecretKey { get; init; }
    public string TriggeredBy { get; init; } = "manual";
}

public sealed record CompetitionSyncResult(
    int ExitCode,
    string? CompetitionId,
    string? CompetitionSlug,
    string? CompetitionName,
    string? ConfigVersion,
    string? EffectiveConfigVersion,
    string? ConfigHash,
    string? PublishedVersionId,
    IReadOnlyList<OperationLogEntry> Logs)
{
    public bool Succeeded => ExitCode == 0;
}

// Syncs a competition definition to Supabase: top-level metadata + lifecycle into
// `competitions`, and the full config verbatim into a `competition_versions` row,
// then auto-publishes by pointing competitions.published_version_id at it.
// Kept separate from the athlete history import (AthleteImporter) on purpose.
public sealed class CompetitionDefinitionImporter
{
    private static readonly JsonSerializerOptions CompetitionJsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly CompetitionConfigValidator configValidator = new();

    public async Task<int> RunAsync(CompetitionSyncOptions opts, CancellationToken ct)
    {
        var result = await SyncAsync(opts, ct);
        WriteLogs(result.Logs);
        return result.ExitCode;
    }

    public async Task<CompetitionSyncResult> SyncAsync(CompetitionSyncOptions opts, CancellationToken ct)
    {
        var logs = new List<OperationLogEntry>
        {
            OperationLogEntry.Info($"Loading competition definition: {opts.CompetitionJsonPath}")
        };

        if (string.IsNullOrWhiteSpace(opts.CompetitionJsonPath))
        {
            logs.Add(OperationLogEntry.Error("competition: --competition-json is required."));
            return Finish(1, logs);
        }

        if (!File.Exists(opts.CompetitionJsonPath))
        {
            logs.Add(OperationLogEntry.Error($"Competition JSON not found: {opts.CompetitionJsonPath}"));
            return Finish(1, logs);
        }

        string rawJson;
        try
        {
            rawJson = await File.ReadAllTextAsync(opts.CompetitionJsonPath, ct);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logs.Add(OperationLogEntry.Error($"Competition JSON could not be read: {ex.Message}"));
            return Finish(1, logs);
        }

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(rawJson);
        }
        catch (JsonException ex)
        {
            logs.Add(OperationLogEntry.Error($"Competition JSON could not be parsed: {ex.Message}"));
            return Finish(1, logs);
        }

        using (doc)
        {
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                logs.Add(OperationLogEntry.Error("Competition JSON root must be an object."));
                return Finish(1, logs);
            }

            if (JsonNode.Parse(rawJson) is not JsonObject configNode)
            {
                logs.Add(OperationLogEntry.Error("Competition JSON root must be an object."));
                return Finish(1, logs);
            }

            Competition? competition;
            try
            {
                competition = JsonSerializer.Deserialize<Competition>(rawJson, CompetitionJsonOptions);
            }
            catch (JsonException ex)
            {
                logs.Add(OperationLogEntry.Error($"Competition JSON could not be parsed as a competition config: {ex.Message}"));
                return Finish(1, logs);
            }

            if (competition is null)
            {
                logs.Add(OperationLogEntry.Error("Competition JSON did not contain a competition config."));
                return Finish(1, logs);
            }

            var validation = configValidator.Validate(competition);
            if (!validation.IsValid)
            {
                logs.Add(OperationLogEntry.Error("Competition config validation failed:"));
                foreach (var error in validation.Errors)
                {
                    logs.Add(OperationLogEntry.Error($"{error.Path}: {error.Message} ({error.Code})"));
                }

                return Finish(
                    1,
                    logs,
                    competition.Id,
                    competition.Slug,
                    competition.Name,
                    competition.ConfigVersion);
            }

            var configHash = CompetitionConfigHasher.Compute(configNode);

            var id = GetString(root, "id");
            var slug = GetString(root, "slug") ?? id;
            var name = GetString(root, "name");
            var version = GetString(root, "configVersion");

            if (string.IsNullOrWhiteSpace(id)
                || string.IsNullOrWhiteSpace(slug)
                || string.IsNullOrWhiteSpace(name)
                || string.IsNullOrWhiteSpace(version))
            {
                logs.Add(OperationLogEntry.Error("Competition JSON must contain id, slug, name and configVersion."));
                return Finish(1, logs, id, slug, name, version, configHash: configHash);
            }

            if (string.IsNullOrWhiteSpace(opts.SupabaseUrl) || string.IsNullOrWhiteSpace(opts.SupabaseSecretKey))
            {
                logs.Add(OperationLogEntry.Error("SUPABASE_URL and SUPABASE_SECRET_KEY must be set."));
                return Finish(2, logs, id, slug, name, version, configHash: configHash);
            }

            var supabase = new SupabaseRestClient(opts.SupabaseUrl, opts.SupabaseSecretKey);

            // 1. Competition metadata must exist before its versions (FK competition_id).
            var status = GetString(root, "status") ?? "upcoming";
            var competitionRow = new JsonObject
            {
                ["id"] = id,
                ["slug"] = slug,
                ["name"] = name,
                ["federation"] = GetString(root, "federation"),
                ["status"] = status,
                ["start_date"] = GetString(root, "startDate"),
                ["end_date"] = GetString(root, "endDate"),
                ["prediction_open_at"] = GetString(root, "predictionOpenAt"),
                ["prediction_lock_at"] = GetString(root, "predictionLockAt"),
                ["summary"] = BuildSummary(opts.CompetitionJsonPath, id!, slug!, logs)
            };
            await supabase.UpsertAsync("public", "competitions", "id", new JsonArray { competitionRow }, ct);
            logs.Add(OperationLogEntry.Info($"Upserted competition '{id}' (status={status})."));

            // 2. Full config verbatim as an immutable versioned JSONB row; capture its id.
            var resolvedVersion = await ResolveOrCreateVersionAsync(
                supabase,
                id!,
                version!,
                configNode,
                configHash,
                logs,
                ct);
            if (resolvedVersion is null)
            {
                return Finish(4, logs, id, slug, name, version, configHash: configHash);
            }

            // 3. Auto-publish: point the competition at this version (no admin UI yet).
            await supabase.PatchAsync(
                "public", "competitions", $"id=eq.{Uri.EscapeDataString(id!)}",
                new JsonObject { ["published_version_id"] = resolvedVersion.Id }, ct);
            logs.Add(OperationLogEntry.Done($"Published '{id}' version '{resolvedVersion.Version}'."));
            return Finish(
                0,
                logs,
                id,
                slug,
                name,
                version,
                resolvedVersion.Version,
                configHash,
                resolvedVersion.Id);
        }
    }

    // The competition list reads a small summary snapshot. Prefer the sibling
    // index.json entry (richer: city/country/tier/modulesCount); otherwise leave
    // it null and let the list fall back to detail fields.
    private static JsonNode? BuildSummary(
        string competitionJsonPath,
        string id,
        string slug,
        ICollection<OperationLogEntry> logs)
    {
        var dir = Path.GetDirectoryName(Path.GetFullPath(competitionJsonPath));
        if (dir is null)
        {
            return null;
        }

        var indexPath = Path.Combine(dir, "index.json");
        if (!File.Exists(indexPath))
        {
            return null;
        }

        try
        {
            if (JsonNode.Parse(File.ReadAllText(indexPath)) is JsonArray entries)
            {
                foreach (var entry in entries)
                {
                    if (entry is JsonObject obj && (Matches(obj, "id", id) || Matches(obj, "slug", slug)))
                    {
                        return obj.DeepClone();
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logs.Add(OperationLogEntry.Warn($"Could not read sibling index.json: {ex.Message}"));
        }

        return null;
    }

    private static bool Matches(JsonObject obj, string key, string value) =>
        obj.TryGetPropertyValue(key, out var node)
        && node is not null
        && string.Equals(node.ToString(), value, StringComparison.OrdinalIgnoreCase);

    private static string? GetString(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static async Task<ResolvedCompetitionVersion?> ResolveOrCreateVersionAsync(
        SupabaseRestClient supabase,
        string competitionId,
        string version,
        JsonNode configNode,
        string configHash,
        ICollection<OperationLogEntry> logs,
        CancellationToken ct)
    {
        var query =
            $"competition_id=eq.{Uri.EscapeDataString(competitionId)}" +
            "&select=id,version,config";
        var existingRows = await supabase.GetAsync("public", "competition_versions", query, ct);
        var existingVersions = existingRows
            .OfType<JsonObject>()
            .Select(row => ToExistingVersion(row, logs))
            .OfType<ExistingCompetitionVersion>()
            .ToArray();

        foreach (var existing in existingVersions)
        {
            if (string.Equals(existing.ConfigHash, configHash, StringComparison.OrdinalIgnoreCase))
            {
                logs.Add(OperationLogEntry.Info(
                    $"competition_version '{existing.Version}' already exists with identical config -> {existing.Id}."));
                return new ResolvedCompetitionVersion(existing.Id, existing.Version, ReusedExisting: true);
            }
        }

        var effectiveVersion = version;
        if (existingVersions.Any(existing => string.Equals(existing.Version, version, StringComparison.OrdinalIgnoreCase)))
        {
            effectiveVersion = BuildDerivedVersion(version, configHash);
            logs.Add(OperationLogEntry.Warn(
                $"Existing competition_version has the same configVersion ('{version}') but different config content. " +
                $"Creating immutable roster-update version '{effectiveVersion}'."));
        }

        var versionRow = new JsonObject
        {
            ["competition_id"] = competitionId,
            ["version"] = effectiveVersion,
            ["config"] = configNode.DeepClone(),
            ["published_at"] = DateTimeOffset.UtcNow.ToString("o")
        };
        var returned = await supabase.InsertReturningAsync(
            "public",
            "competition_versions",
            new JsonArray { versionRow },
            ct);
        if (returned.Count == 0 || returned[0]?["id"] is null)
        {
            logs.Add(OperationLogEntry.Error("competition_versions insert returned no id."));
            return null;
        }

        var versionId = returned[0]!["id"]!.ToString();
        logs.Add(OperationLogEntry.Info($"Inserted competition_version '{effectiveVersion}' -> {versionId}."));
        return new ResolvedCompetitionVersion(versionId, effectiveVersion, ReusedExisting: false);
    }

    private static ExistingCompetitionVersion? ToExistingVersion(
        JsonObject row,
        ICollection<OperationLogEntry> logs)
    {
        var id = row["id"]?.ToString();
        var version = row["version"]?.ToString();
        var config = row["config"];
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(version) || config is null)
        {
            logs.Add(OperationLogEntry.Error("Existing competition_version row is missing id, version or config."));
            return null;
        }

        return new ExistingCompetitionVersion(
            id,
            version,
            CompetitionConfigHasher.Compute(config));
    }

    private static string BuildDerivedVersion(string version, string configHash)
    {
        var suffix = configHash[..Math.Min(8, configHash.Length)];
        return $"{version}+roster.{suffix}";
    }

    private static CompetitionSyncResult Finish(
        int exitCode,
        List<OperationLogEntry> logs,
        string? competitionId = null,
        string? competitionSlug = null,
        string? competitionName = null,
        string? configVersion = null,
        string? effectiveConfigVersion = null,
        string? configHash = null,
        string? publishedVersionId = null) => new(
            exitCode,
            competitionId,
            competitionSlug,
            competitionName,
            configVersion,
            effectiveConfigVersion,
            configHash,
            publishedVersionId,
            logs.ToArray());

    private static void WriteLogs(IEnumerable<OperationLogEntry> logs)
    {
        foreach (var log in logs)
        {
            var line = $"[{log.Level}] {log.Message}";
            if (string.Equals(log.Level, "error", StringComparison.OrdinalIgnoreCase))
            {
                Console.Error.WriteLine(line);
            }
            else
            {
                Console.WriteLine(line);
            }
        }
    }

    private sealed record ExistingCompetitionVersion(string Id, string Version, string ConfigHash);

    private sealed record ResolvedCompetitionVersion(string Id, string Version, bool ReusedExisting);
}

public static class CompetitionConfigHasher
{
    public static string Compute(JsonNode config)
    {
        var canonical = Normalize(config).ToJsonString();
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static JsonNode Normalize(JsonNode node)
    {
        if (node is JsonObject obj)
        {
            var normalized = new JsonObject();
            foreach (var property in obj.OrderBy(property => property.Key, StringComparer.Ordinal))
            {
                normalized[property.Key] = property.Value is null
                    ? null
                    : Normalize(property.Value);
            }

            return normalized;
        }

        if (node is JsonArray arr)
        {
            var normalized = new JsonArray();
            foreach (var item in arr)
            {
                normalized.Add(item is null ? null : Normalize(item));
            }

            return normalized;
        }

        return node.DeepClone();
    }
}
