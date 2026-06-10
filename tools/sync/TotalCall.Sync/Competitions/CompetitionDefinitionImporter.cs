using System.Text.Json;
using System.Text.Json.Nodes;

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

        // 2. Full config verbatim as a versioned JSONB row; capture its id.
        var versionRow = new JsonObject
        {
            ["competition_id"] = id,
            ["version"] = version,
            ["config"] = JsonNode.Parse(rawJson),
            ["published_at"] = DateTimeOffset.UtcNow.ToString("o")
        };
        var returned = await supabase.UpsertReturningAsync(
            "public", "competition_versions", "competition_id,version",
            new JsonArray { versionRow }, ct);
        if (returned.Count == 0 || returned[0]?["id"] is null)
        {
            Console.Error.WriteLine("[error] competition_versions upsert returned no id.");
            return 3;
        }
        var versionId = returned[0]!["id"]!.ToString();
        Console.WriteLine($"[info] Upserted competition_version '{version}' -> {versionId}.");

        // 3. Auto-publish: point the competition at this version (no admin UI yet).
        await supabase.PatchAsync(
            "public", "competitions", $"id=eq.{Uri.EscapeDataString(id!)}",
            new JsonObject { ["published_version_id"] = versionId }, ct);
        Console.WriteLine($"[done] Published '{id}' version '{version}'.");
        return 0;
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
}
