using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using TotalCall.Core.Domain.Competitions;
using TotalCall.Core.Validation;

namespace TotalCall.Sync.Competitions;

public sealed class CompetitionSyncOptions
{
    public required string CompetitionJsonPath { get; init; }
    public string? SupabaseUrl { get; init; }
    public string? SupabaseSecretKey { get; init; }
    public string TriggeredBy { get; init; } = "manual";
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
        Console.WriteLine($"[info] Loading competition definition: {opts.CompetitionJsonPath}");
        var rawJson = await File.ReadAllTextAsync(opts.CompetitionJsonPath, ct);

        using var doc = JsonDocument.Parse(rawJson);
        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            Console.Error.WriteLine("[error] Competition JSON root must be an object.");
            return 1;
        }

        var configNode = JsonNode.Parse(rawJson);
        if (configNode is not JsonObject)
        {
            Console.Error.WriteLine("[error] Competition JSON root must be an object.");
            return 1;
        }

        var competition = DeserializeCompetition(rawJson);
        if (competition is null)
        {
            return 1;
        }

        var validation = configValidator.Validate(competition);
        if (!validation.IsValid)
        {
            Console.Error.WriteLine("[error] Competition config validation failed:");
            foreach (var error in validation.Errors)
            {
                Console.Error.WriteLine($"[error] {error.Path}: {error.Message} ({error.Code})");
            }

            return 1;
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
            Console.Error.WriteLine("[error] Competition JSON must contain id, slug, name and configVersion.");
            return 1;
        }

        if (string.IsNullOrWhiteSpace(opts.SupabaseUrl) || string.IsNullOrWhiteSpace(opts.SupabaseSecretKey))
        {
            Console.Error.WriteLine("[error] SUPABASE_URL and SUPABASE_SECRET_KEY must be set.");
            return 2;
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
            ["summary"] = BuildSummary(opts.CompetitionJsonPath, id!, slug!)
        };
        await supabase.UpsertAsync("public", "competitions", "id", new JsonArray { competitionRow }, ct);
        Console.WriteLine($"[info] Upserted competition '{id}' (status={status}).");

        // 2. Full config verbatim as an immutable versioned JSONB row; capture its id.
        var versionId = await ResolveOrCreateVersionAsync(
            supabase,
            id!,
            version!,
            configNode,
            configHash,
            ct);
        if (versionId is null)
        {
            return 4;
        }

        // 3. Auto-publish: point the competition at this version (no admin UI yet).
        await supabase.PatchAsync(
            "public", "competitions", $"id=eq.{Uri.EscapeDataString(id!)}",
            new JsonObject { ["published_version_id"] = versionId }, ct);
        Console.WriteLine($"[done] Published '{id}' version '{version}'.");
        return 0;
    }

    private static Competition? DeserializeCompetition(string rawJson)
    {
        try
        {
            return JsonSerializer.Deserialize<Competition>(rawJson, CompetitionJsonOptions);
        }
        catch (JsonException ex)
        {
            Console.Error.WriteLine($"[error] Competition JSON could not be parsed as a competition config: {ex.Message}");
            return null;
        }
    }

    // The competition list reads a small summary snapshot. Prefer the sibling
    // index.json entry (richer: city/country/tier/modulesCount); otherwise leave
    // it null and let the list fall back to detail fields.
    private static JsonNode? BuildSummary(string competitionJsonPath, string id, string slug)
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
            Console.WriteLine($"[warn] Could not read sibling index.json: {ex.Message}");
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

    private static async Task<string?> ResolveOrCreateVersionAsync(
        SupabaseRestClient supabase,
        string competitionId,
        string version,
        JsonNode configNode,
        string configHash,
        CancellationToken ct)
    {
        var query =
            $"competition_id=eq.{Uri.EscapeDataString(competitionId)}" +
            "&select=id,version,config";
        var existingRows = await supabase.GetAsync("public", "competition_versions", query, ct);
        var existingVersions = existingRows
            .OfType<JsonObject>()
            .Select(ToExistingVersion)
            .OfType<ExistingCompetitionVersion>()
            .ToArray();

        foreach (var existing in existingVersions)
        {
            if (string.Equals(existing.ConfigHash, configHash, StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine(
                    $"[info] competition_version '{existing.Version}' already exists with identical config -> {existing.Id}.");
                return existing.Id;
            }
        }

        var effectiveVersion = version;
        if (existingVersions.Any(existing => string.Equals(existing.Version, version, StringComparison.OrdinalIgnoreCase)))
        {
            effectiveVersion = BuildDerivedVersion(version, configHash);
            Console.WriteLine(
                "[warn] Existing competition_version has the same configVersion " +
                $"('{version}') but different config content. " +
                $"Creating immutable roster-update version '{effectiveVersion}'.");
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
            Console.Error.WriteLine("[error] competition_versions insert returned no id.");
            return null;
        }

        var versionId = returned[0]!["id"]!.ToString();
        Console.WriteLine($"[info] Inserted competition_version '{effectiveVersion}' -> {versionId}.");
        return versionId;
    }

    private static ExistingCompetitionVersion? ToExistingVersion(JsonObject row)
    {
        var id = row["id"]?.ToString();
        var version = row["version"]?.ToString();
        var config = row["config"];
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(version) || config is null)
        {
            Console.Error.WriteLine("[error] Existing competition_version row is missing id, version or config.");
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

    private sealed record ExistingCompetitionVersion(string Id, string Version, string ConfigHash);
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
