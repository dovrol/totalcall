using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace TotalCall.OplImporter;

public sealed class ImporterOptions
{
    public required string CompetitionJsonPath { get; init; }
    public required string Source { get; init; }
    public string? CsvUrl { get; init; }
    public string? LocalCsvPath { get; init; }
    public bool DryRun { get; init; }
    public string? SupabaseUrl { get; init; }
    public string? SupabaseSecretKey { get; init; }
    public int BatchSize { get; init; } = 500;
    public string TriggeredBy { get; init; } = "manual";
}

public sealed class Importer
{
    private const int LookupBatchSize = 100;

    public async Task<int> RunAsync(ImporterOptions opts, CancellationToken ct)
    {
        // 1. Load competition JSON
        Console.WriteLine($"[info] Loading competition: {opts.CompetitionJsonPath}");
        var definition = await LoadCompetitionAsync(opts.CompetitionJsonPath, ct);
        Console.WriteLine($"[info] Competition: {definition.Name} ({definition.Athletes.Count} athletes)");

        // 2. Build alias map from externalAthleteRefs (filtered by requested source)
        var roster = new List<CompetitionAthlete>();
        var nameIndex = new Dictionary<string, string>(StringComparer.Ordinal); // normalized name -> athlete slug
        var externalIdIndex = new Dictionary<string, string>(StringComparer.Ordinal); // external_id -> slug
        var skippedNoRefs = 0;

        foreach (var a in definition.Athletes)
        {
            var refs = a.ExternalAthleteRefs.Where(r => r.Source == opts.Source).ToList();
            if (refs.Count == 0)
            {
                Console.WriteLine($"[warn] Athlete '{a.Id}' has no externalAthleteRefs for source '{opts.Source}' — skipping");
                skippedNoRefs++;
                continue;
            }
            roster.Add(a);
            foreach (var r in refs)
            {
                var norm = NameNormalizer.Normalize(r.Name);
                if (norm is not null && !nameIndex.ContainsKey(norm))
                {
                    nameIndex[norm] = a.Id;
                }
                if (!string.IsNullOrWhiteSpace(r.ExternalId) && !externalIdIndex.ContainsKey(r.ExternalId))
                {
                    externalIdIndex[r.ExternalId] = a.Id;
                }
            }
        }

        Console.WriteLine($"[info] Roster: {roster.Count} athletes / {nameIndex.Count} name aliases ({skippedNoRefs} skipped)");
        if (roster.Count == 0)
        {
            Console.Error.WriteLine($"[error] No athletes have externalAthleteRefs for source '{opts.Source}'. Nothing to import.");
            return 1;
        }

        // ---- Dry-run early branch: scan CSV, count matches, exit without DB writes ----
        if (opts.DryRun)
        {
            var dryCounters = await ScanCsvAsync(opts, nameIndex, ct);
            Console.WriteLine($"[dry-run] processed={dryCounters.RowsProcessed} matched={dryCounters.RowsMatched} failed={dryCounters.RowsFailed}");
            return 0;
        }

        if (string.IsNullOrWhiteSpace(opts.SupabaseUrl) || string.IsNullOrWhiteSpace(opts.SupabaseSecretKey))
        {
            Console.Error.WriteLine("[error] SUPABASE_URL and SUPABASE_SECRET_KEY must be set for non-dry-run.");
            return 2;
        }

        var supabase = new SupabaseRestClient(opts.SupabaseUrl, opts.SupabaseSecretKey);

        // 3. Resolve source_id
        var sources = await supabase.GetAsync("public", "data_sources",
            $"code=eq.{Uri.EscapeDataString(opts.Source)}&select=id", ct);
        if (sources.Count == 0)
        {
            Console.Error.WriteLine($"[error] data_sources has no row for code '{opts.Source}'.");
            return 3;
        }
        var sourceId = sources[0]!["id"]!.ToString();
        Console.WriteLine($"[info] Resolved data_source '{opts.Source}' -> {sourceId}");

        // 4. Upsert athletes from competition JSON; fetch slug -> id map
        await UpsertAthletesAsync(supabase, roster, ct);
        var slugToId = await FetchAthleteIdsAsync(supabase, roster.Select(a => a.Id).Distinct().ToList(), ct);
        Console.WriteLine($"[info] Resolved {slugToId.Count}/{roster.Count} athletes in DB");

        // 5. Upsert aliases + external_ids
        await UpsertAliasesAsync(supabase, roster, slugToId, opts.Source, ct);
        await UpsertExternalIdsAsync(supabase, roster, slugToId, sourceId, opts.Source, ct);

        // 6. Start import_run
        var importRunId = await StartImportRunAsync(supabase, sourceId, opts, ct);
        Console.WriteLine($"[info] Started import_run {importRunId}");

        var counters = new ImportCounters();
        var errors = new List<JsonObject>();

        try
        {
            // 7. Stream + filter CSV
            Console.WriteLine("[info] Opening CSV stream...");
            var csvPath = await EnsureCsvAsync(opts, ct);
            Console.WriteLine($"[info] CSV ready: {csvPath}");

            var meetsByKey = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
            var resultsToWrite = new List<JsonObject>();
            var resultsAthleteSlug = new List<string>();
            var resultsMeetKey = new List<string>();

            using (var reader = new StreamReader(csvPath, Encoding.UTF8))
            {
                var rowIndex = 0;
                foreach (var row in CsvReader.Read(reader))
                {
                    rowIndex++;
                    counters.RowsProcessed++;
                    try
                    {
                        var name = row.GetValueOrDefault("Name", "");
                        var norm = NameNormalizer.Normalize(name);
                        if (norm is null || !nameIndex.TryGetValue(norm, out var athleteSlug))
                        {
                            continue;
                        }

                        counters.RowsMatched++;
                        var opl = ParseOplRow(row);

                        var meetKey = BuildMeetRecordKey(opl);
                        if (!meetsByKey.ContainsKey(meetKey))
                        {
                            meetsByKey[meetKey] = BuildMeetJson(opl, sourceId, meetKey);
                        }

                        var resultKey = BuildResultRecordKey(opl, norm);
                        var (hash, payload) = BuildResultPayload(opl, sourceId, athleteSlug, resultKey, importRunId);
                        resultsToWrite.Add(payload);
                        resultsAthleteSlug.Add(athleteSlug);
                        resultsMeetKey.Add(meetKey);
                    }
                    catch (Exception ex)
                    {
                        counters.RowsFailed++;
                        errors.Add(new JsonObject
                        {
                            ["run_id"] = importRunId,
                            ["row_index"] = rowIndex,
                            ["error_code"] = "row_parse",
                            ["error_message"] = ex.Message,
                            ["raw_row"] = SerializeRow(row)
                        });
                    }
                }
            }

            Console.WriteLine($"[info] Scan complete: processed={counters.RowsProcessed} matched={counters.RowsMatched} failed={counters.RowsFailed}");
            Console.WriteLine($"[info] Unique meets: {meetsByKey.Count}; results to consider: {resultsToWrite.Count}");

            // 8. Upsert source_meets, build meet_record_key -> id map
            var meetKeyToId = await UpsertMeetsAsync(supabase, meetsByKey.Values.ToList(), opts.BatchSize, ct);

            // 9. Fill athlete_id / source_meet_id; pre-fetch existing hashes
            for (var i = 0; i < resultsToWrite.Count; i++)
            {
                resultsToWrite[i]["athlete_id"] = slugToId[resultsAthleteSlug[i]];
                if (meetKeyToId.TryGetValue(resultsMeetKey[i], out var meetId))
                {
                    resultsToWrite[i]["source_meet_id"] = meetId;
                }
            }

            await UpsertResultsAsync(supabase, sourceId, resultsToWrite, counters, opts.BatchSize, ct);

            // 10. Persist errors
            if (errors.Count > 0)
            {
                await WriteErrorsAsync(supabase, errors, opts.BatchSize, ct);
            }

            await FinishImportRunAsync(supabase, importRunId, "success", counters, opts, null, ct);
            Console.WriteLine($"[done] inserted={counters.RowsInserted} updated={counters.RowsUpdated} skipped={counters.RowsSkipped} deduplicated={counters.RowsDeduplicated} conflicting_duplicates={counters.RowsConflictingDuplicates} failed={counters.RowsFailed}");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[fatal] {ex}");
            await FinishImportRunAsync(supabase, importRunId, "failed", counters, opts, ex.Message, ct);
            return 10;
        }
    }

    // ============================================================
    // Competition JSON loading
    // ============================================================
    private static async Task<CompetitionDefinition> LoadCompetitionAsync(string path, CancellationToken ct)
    {
        await using var fs = File.OpenRead(path);
        var def = await JsonSerializer.DeserializeAsync<CompetitionDefinition>(fs,
            new JsonSerializerOptions(JsonSerializerDefaults.Web), ct);
        return def ?? throw new InvalidOperationException("Could not parse competition JSON");
    }

    // ============================================================
    // CSV acquisition (URL/ZIP or local file)
    // ============================================================
    private static async Task<string> EnsureCsvAsync(ImporterOptions opts, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(opts.LocalCsvPath))
        {
            return opts.LocalCsvPath;
        }

        var url = opts.CsvUrl ?? DefaultCsvUrl(opts.Source);
        Console.WriteLine($"[info] Downloading {url}");
        var tmpZip = Path.GetTempFileName();
        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(30) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("TotalCall-OplImporter/1.0");
        await using (var src = await http.GetStreamAsync(url, ct))
        await using (var dst = File.Create(tmpZip))
        {
            await src.CopyToAsync(dst, ct);
        }

        var tmpCsv = Path.Combine(Path.GetTempPath(), $"opl-{Guid.NewGuid():N}.csv");
        using (var zip = ZipFile.OpenRead(tmpZip))
        {
            var entry = zip.Entries.FirstOrDefault(e => e.FullName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException("No .csv in archive");
            entry.ExtractToFile(tmpCsv, overwrite: true);
        }
        File.Delete(tmpZip);
        return tmpCsv;
    }

    private static string DefaultCsvUrl(string source) => source switch
    {
        "openipf" => "https://openpowerlifting.gitlab.io/opl-csv/files/openipf-latest.zip",
        "openpowerlifting" => "https://openpowerlifting.gitlab.io/opl-csv/files/openpowerlifting-latest.zip",
        _ => throw new ArgumentException($"No default CSV URL for source '{source}'. Pass --csv-url.")
    };

    // ============================================================
    // Dry-run scan (no DB writes)
    // ============================================================
    private static async Task<ImportCounters> ScanCsvAsync(
        ImporterOptions opts,
        IReadOnlyDictionary<string, string> nameIndex,
        CancellationToken ct)
    {
        var csvPath = await EnsureCsvAsync(opts, ct);
        var counters = new ImportCounters();
        using var reader = new StreamReader(csvPath);
        foreach (var row in CsvReader.Read(reader))
        {
            counters.RowsProcessed++;
            var name = row.GetValueOrDefault("Name", "");
            var norm = NameNormalizer.Normalize(name);
            if (norm is not null && nameIndex.ContainsKey(norm))
            {
                counters.RowsMatched++;
            }
        }
        return counters;
    }

    // ============================================================
    // OPL row parsing
    // ============================================================
    private static OplRow ParseOplRow(IReadOnlyDictionary<string, string> r)
    {
        return new OplRow
        {
            Name = r.GetValueOrDefault("Name", ""),
            Sex = r.GetValueOrDefault("Sex"),
            Country = NullIfEmpty(r.GetValueOrDefault("Country")),
            Event = NullIfEmpty(r.GetValueOrDefault("Event")),
            Equipment = NullIfEmpty(r.GetValueOrDefault("Equipment")),
            Division = NullIfEmpty(r.GetValueOrDefault("Division")),
            Tested = ParseTested(r.GetValueOrDefault("Tested")),
            Age = ParseDecimal(r.GetValueOrDefault("Age")),
            AgeClass = NullIfEmpty(r.GetValueOrDefault("AgeClass")),
            BirthYearClass = NullIfEmpty(r.GetValueOrDefault("BirthYearClass")),
            BodyweightKg = ParseDecimal(r.GetValueOrDefault("BodyweightKg")),
            WeightClassKg = NullIfEmpty(r.GetValueOrDefault("WeightClassKg")),
            Squat1Kg = ParseDecimal(r.GetValueOrDefault("Squat1Kg")),
            Squat2Kg = ParseDecimal(r.GetValueOrDefault("Squat2Kg")),
            Squat3Kg = ParseDecimal(r.GetValueOrDefault("Squat3Kg")),
            Squat4Kg = ParseDecimal(r.GetValueOrDefault("Squat4Kg")),
            BestSquatKg = ParseDecimal(r.GetValueOrDefault("Best3SquatKg") ?? r.GetValueOrDefault("BestSquatKg")),
            Bench1Kg = ParseDecimal(r.GetValueOrDefault("Bench1Kg")),
            Bench2Kg = ParseDecimal(r.GetValueOrDefault("Bench2Kg")),
            Bench3Kg = ParseDecimal(r.GetValueOrDefault("Bench3Kg")),
            Bench4Kg = ParseDecimal(r.GetValueOrDefault("Bench4Kg")),
            BestBenchKg = ParseDecimal(r.GetValueOrDefault("Best3BenchKg") ?? r.GetValueOrDefault("BestBenchKg")),
            Deadlift1Kg = ParseDecimal(r.GetValueOrDefault("Deadlift1Kg")),
            Deadlift2Kg = ParseDecimal(r.GetValueOrDefault("Deadlift2Kg")),
            Deadlift3Kg = ParseDecimal(r.GetValueOrDefault("Deadlift3Kg")),
            Deadlift4Kg = ParseDecimal(r.GetValueOrDefault("Deadlift4Kg")),
            BestDeadliftKg = ParseDecimal(r.GetValueOrDefault("Best3DeadliftKg") ?? r.GetValueOrDefault("BestDeadliftKg")),
            TotalKg = ParseDecimal(r.GetValueOrDefault("TotalKg")),
            Place = NullIfEmpty(r.GetValueOrDefault("Place")),
            Dots = ParseDecimal(r.GetValueOrDefault("Dots")),
            Wilks = ParseDecimal(r.GetValueOrDefault("Wilks")),
            Glossbrenner = ParseDecimal(r.GetValueOrDefault("Glossbrenner")),
            Goodlift = ParseDecimal(r.GetValueOrDefault("Goodlift")),
            MeetName = NullIfEmpty(r.GetValueOrDefault("MeetName")),
            Date = ParseDate(r.GetValueOrDefault("Date")),
            Federation = NullIfEmpty(r.GetValueOrDefault("Federation")),
            ParentFederation = NullIfEmpty(r.GetValueOrDefault("ParentFederation")),
            MeetCountry = NullIfEmpty(r.GetValueOrDefault("MeetCountry")),
            MeetState = NullIfEmpty(r.GetValueOrDefault("MeetState")),
            MeetTown = NullIfEmpty(r.GetValueOrDefault("MeetTown"))
        };
    }

    private static bool? ParseTested(string? s) => s switch
    {
        null or "" => null,
        "Yes" => true,
        _ => false
    };

    private static decimal? ParseDecimal(string? s)
    {
        if (string.IsNullOrEmpty(s)) return null;
        return decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out var d) ? d : null;
    }

    private static DateOnly? ParseDate(string? s)
    {
        if (string.IsNullOrEmpty(s)) return null;
        return DateOnly.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;
    }

    private static string? NullIfEmpty(string? s) => string.IsNullOrEmpty(s) ? null : s;

    // ============================================================
    // Record-key + hash construction
    // ============================================================
    private static string BuildMeetRecordKey(OplRow r)
    {
        var name = NameNormalizer.Normalize(r.MeetName) ?? "";
        var date = r.Date?.ToString("yyyy-MM-dd") ?? "";
        var fed = r.Federation ?? "";
        return $"{name}|{date}|{fed}";
    }

    private static string BuildResultRecordKey(OplRow r, string normalizedName)
    {
        var date = r.Date?.ToString("yyyy-MM-dd") ?? "";
        var meet = NameNormalizer.Normalize(r.MeetName) ?? "";
        var fed = r.Federation ?? "";
        var ev = r.Event ?? "";
        var eq = r.Equipment ?? "";
        var div = r.Division ?? "";
        return $"{normalizedName}|{date}|{meet}|{fed}|{ev}|{eq}|{div}";
    }

    private static string Sha256(string s)
    {
        using var sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(s))).ToLowerInvariant();
    }

    // ============================================================
    // JSON row builders
    // ============================================================
    private static JsonObject BuildMeetJson(OplRow r, string sourceId, string meetKey)
    {
        return new JsonObject
        {
            ["source_id"] = sourceId,
            ["source_record_key"] = meetKey,
            ["name"] = r.MeetName ?? "(unknown meet)",
            ["date"] = r.Date?.ToString("yyyy-MM-dd"),
            ["federation"] = r.Federation,
            ["parent_federation"] = r.ParentFederation,
            ["country"] = r.MeetCountry,
            ["state"] = r.MeetState,
            ["town"] = r.MeetTown,
            ["tested"] = r.Tested
        };
    }

    private static (string hash, JsonObject payload) BuildResultPayload(
        OplRow r,
        string sourceId,
        string athleteSlug,
        string recordKey,
        string importRunId)
    {
        // Deterministic content for hash (excludes audit fields).
        var hashSource = string.Join("|", new[]
        {
            r.Event, r.Equipment, r.Division, r.AgeClass, r.BirthYearClass,
            r.Age?.ToString(CultureInfo.InvariantCulture),
            r.BodyweightKg?.ToString(CultureInfo.InvariantCulture),
            r.WeightClassKg,
            r.Squat1Kg?.ToString(CultureInfo.InvariantCulture),
            r.Squat2Kg?.ToString(CultureInfo.InvariantCulture),
            r.Squat3Kg?.ToString(CultureInfo.InvariantCulture),
            r.Squat4Kg?.ToString(CultureInfo.InvariantCulture),
            r.BestSquatKg?.ToString(CultureInfo.InvariantCulture),
            r.Bench1Kg?.ToString(CultureInfo.InvariantCulture),
            r.Bench2Kg?.ToString(CultureInfo.InvariantCulture),
            r.Bench3Kg?.ToString(CultureInfo.InvariantCulture),
            r.Bench4Kg?.ToString(CultureInfo.InvariantCulture),
            r.BestBenchKg?.ToString(CultureInfo.InvariantCulture),
            r.Deadlift1Kg?.ToString(CultureInfo.InvariantCulture),
            r.Deadlift2Kg?.ToString(CultureInfo.InvariantCulture),
            r.Deadlift3Kg?.ToString(CultureInfo.InvariantCulture),
            r.Deadlift4Kg?.ToString(CultureInfo.InvariantCulture),
            r.BestDeadliftKg?.ToString(CultureInfo.InvariantCulture),
            r.TotalKg?.ToString(CultureInfo.InvariantCulture),
            r.Place,
            r.Dots?.ToString(CultureInfo.InvariantCulture),
            r.Wilks?.ToString(CultureInfo.InvariantCulture),
            r.Glossbrenner?.ToString(CultureInfo.InvariantCulture),
            r.Goodlift?.ToString(CultureInfo.InvariantCulture),
            r.Tested?.ToString()
        });
        var hash = Sha256(hashSource);

        var obj = new JsonObject
        {
            ["source_id"] = sourceId,
            ["source_record_key"] = recordKey,
            ["source_row_hash"] = hash,
            ["meet_date"] = r.Date?.ToString("yyyy-MM-dd"),
            ["meet_name"] = r.MeetName,
            ["federation"] = r.Federation,
            ["event"] = r.Event,
            ["equipment"] = r.Equipment,
            ["division"] = r.Division,
            ["age_class"] = r.AgeClass,
            ["birth_year_class"] = r.BirthYearClass,
            ["age"] = r.Age,
            ["bodyweight_kg"] = r.BodyweightKg,
            ["weight_class_kg"] = r.WeightClassKg,
            ["squat1_kg"] = r.Squat1Kg,
            ["squat2_kg"] = r.Squat2Kg,
            ["squat3_kg"] = r.Squat3Kg,
            ["squat4_kg"] = r.Squat4Kg,
            ["best_squat_kg"] = r.BestSquatKg,
            ["bench1_kg"] = r.Bench1Kg,
            ["bench2_kg"] = r.Bench2Kg,
            ["bench3_kg"] = r.Bench3Kg,
            ["bench4_kg"] = r.Bench4Kg,
            ["best_bench_kg"] = r.BestBenchKg,
            ["deadlift1_kg"] = r.Deadlift1Kg,
            ["deadlift2_kg"] = r.Deadlift2Kg,
            ["deadlift3_kg"] = r.Deadlift3Kg,
            ["deadlift4_kg"] = r.Deadlift4Kg,
            ["best_deadlift_kg"] = r.BestDeadliftKg,
            ["total_kg"] = r.TotalKg,
            ["place"] = r.Place,
            ["place_numeric"] = int.TryParse(r.Place, out var p) ? p : null,
            ["dots_points"] = r.Dots,
            ["wilks_points"] = r.Wilks,
            ["glossbrenner_points"] = r.Glossbrenner,
            ["goodlift_points"] = r.Goodlift,
            ["tested"] = r.Tested,
            ["import_run_id"] = importRunId
        };
        // athlete_slug is not a column; caller fills athlete_id after slug->id resolution.
        return (hash, obj);
    }

    private static JsonObject SerializeRow(IReadOnlyDictionary<string, string> row)
    {
        var o = new JsonObject();
        foreach (var kv in row) o[kv.Key] = kv.Value;
        return o;
    }

    // ============================================================
    // Supabase: athletes, aliases, external_ids
    // ============================================================
    private static async Task UpsertAthletesAsync(
        SupabaseRestClient supa,
        IReadOnlyList<CompetitionAthlete> roster,
        CancellationToken ct)
    {
        var rows = new JsonArray();
        foreach (var a in roster)
        {
            rows.Add(new JsonObject
            {
                ["slug"] = a.Id,
                ["display_name"] = a.DisplayName,
                ["sex"] = MapSex(a.Sex),
                ["country_code"] = a.CountryCode,
                ["country_name"] = a.CountryName
            });
        }
        await supa.UpsertAsync("public", "athletes", "slug", rows, ct);
    }

    private static string MapSex(string? sex) => sex?.ToLowerInvariant() switch
    {
        "female" or "f" => "female",
        "male" or "m" => "male",
        "mx" => "mx",
        _ => "unspecified"
    };

    private static async Task<Dictionary<string, string>> FetchAthleteIdsAsync(
        SupabaseRestClient supa,
        IReadOnlyList<string> slugs,
        CancellationToken ct)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var chunk in slugs.Chunk(LookupBatchSize))
        {
            var list = string.Join(",", chunk.Select(Uri.EscapeDataString));
            var arr = await supa.GetAsync("public", "athletes",
                $"slug=in.({list})&select=id,slug", ct);
            foreach (var node in arr)
            {
                if (node is null) continue;
                map[node["slug"]!.ToString()] = node["id"]!.ToString();
            }
        }
        return map;
    }

    private static async Task UpsertAliasesAsync(
        SupabaseRestClient supa,
        IReadOnlyList<CompetitionAthlete> roster,
        IReadOnlyDictionary<string, string> slugToId,
        string source,
        CancellationToken ct)
    {
        var rows = new JsonArray();
        foreach (var a in roster)
        {
            if (!slugToId.TryGetValue(a.Id, out var athleteId)) continue;
            foreach (var r in a.ExternalAthleteRefs.Where(x => x.Source == source))
            {
                rows.Add(new JsonObject
                {
                    ["athlete_id"] = athleteId,
                    ["alias_name"] = r.Name,
                    ["source"] = r.Source
                });
            }
        }
        if (rows.Count == 0) return;
        await supa.UpsertAsync("public", "athlete_aliases", "athlete_id,alias_name", rows, ct);
    }

    private static async Task UpsertExternalIdsAsync(
        SupabaseRestClient supa,
        IReadOnlyList<CompetitionAthlete> roster,
        IReadOnlyDictionary<string, string> slugToId,
        string sourceId,
        string source,
        CancellationToken ct)
    {
        var rows = new JsonArray();
        foreach (var a in roster)
        {
            if (!slugToId.TryGetValue(a.Id, out var athleteId)) continue;
            foreach (var r in a.ExternalAthleteRefs.Where(x => x.Source == source && !string.IsNullOrWhiteSpace(x.ExternalId)))
            {
                rows.Add(new JsonObject
                {
                    ["athlete_id"] = athleteId,
                    ["source_id"] = sourceId,
                    ["external_id"] = r.ExternalId!
                });
            }
        }
        if (rows.Count == 0) return;
        await supa.UpsertAsync("public", "athlete_external_ids", "source_id,external_id", rows, ct);
    }

    // ============================================================
    // Supabase: meets
    // ============================================================
    private static async Task<Dictionary<string, string>> UpsertMeetsAsync(
        SupabaseRestClient supa,
        IReadOnlyList<JsonObject> meets,
        int batchSize,
        CancellationToken ct)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        if (meets.Count == 0) return map;

        foreach (var chunk in meets.Chunk(batchSize))
        {
            var arr = new JsonArray();
            foreach (var m in chunk) arr.Add(m.DeepClone());
            var returned = await supa.UpsertReturningAsync("public", "source_meets",
                "source_id,source_record_key", arr, ct);
            foreach (var node in returned)
            {
                if (node is null) continue;
                map[node["source_record_key"]!.ToString()] = node["id"]!.ToString();
            }
        }
        Console.WriteLine($"[info] Upserted {map.Count} meets");
        return map;
    }

    // ============================================================
    // Supabase: results (with hash diff for accurate counters)
    // ============================================================
    private static async Task UpsertResultsAsync(
        SupabaseRestClient supa,
        string sourceId,
        IReadOnlyList<JsonObject> results,
        ImportCounters counters,
        int batchSize,
        CancellationToken ct)
    {
        if (results.Count == 0) return;

        var uniqueResults = DeduplicateResults(results, counters);

        // Pre-fetch existing (source_record_key -> source_row_hash) in lookup chunks
        var existing = new Dictionary<string, string>(StringComparer.Ordinal);
        var keys = uniqueResults.Select(r => r["source_record_key"]!.ToString()).ToList();
        foreach (var chunk in keys.Chunk(LookupBatchSize))
        {
            var list = string.Join(",", chunk.Select(k => $"\"{k.Replace("\"", "\\\"")}\""));
            var arr = await supa.GetAsync("public", "athlete_results",
                $"source_id=eq.{sourceId}&source_record_key=in.({list})&select=source_record_key,source_row_hash", ct);
            foreach (var node in arr)
            {
                if (node is null) continue;
                existing[node["source_record_key"]!.ToString()] = node["source_row_hash"]?.ToString() ?? "";
            }
        }

        var toWrite = new List<JsonObject>(uniqueResults.Count);
        foreach (var r in uniqueResults)
        {
            var key = r["source_record_key"]!.ToString();
            var hash = r["source_row_hash"]!.ToString();
            if (existing.TryGetValue(key, out var existingHash))
            {
                if (existingHash == hash)
                {
                    counters.RowsSkipped++;
                    continue;
                }
                counters.RowsUpdated++;
            }
            else
            {
                counters.RowsInserted++;
            }
            toWrite.Add(r);
        }

        foreach (var chunk in toWrite.Chunk(batchSize))
        {
            var arr = new JsonArray();
            foreach (var r in chunk) arr.Add(r.DeepClone());
            await supa.UpsertAsync("public", "athlete_results", "source_id,source_record_key", arr, ct);
        }
        Console.WriteLine($"[info] Results written: inserted={counters.RowsInserted} updated={counters.RowsUpdated} skipped={counters.RowsSkipped} deduplicated={counters.RowsDeduplicated} conflicting_duplicates={counters.RowsConflictingDuplicates}");
    }

    private static IReadOnlyList<JsonObject> DeduplicateResults(
        IReadOnlyList<JsonObject> results,
        ImportCounters counters)
    {
        var resultsByKey = new Dictionary<string, JsonObject>(StringComparer.Ordinal);

        foreach (var result in results)
        {
            var key = result["source_record_key"]!.ToString();
            if (!resultsByKey.TryGetValue(key, out var existing))
            {
                resultsByKey[key] = result;
                continue;
            }

            counters.RowsDeduplicated++;

            var existingHash = existing["source_row_hash"]!.ToString();
            var candidateHash = result["source_row_hash"]!.ToString();
            if (!string.Equals(existingHash, candidateHash, StringComparison.Ordinal))
            {
                counters.RowsConflictingDuplicates++;
                if (string.Compare(candidateHash, existingHash, StringComparison.Ordinal) < 0)
                {
                    resultsByKey[key] = result;
                }
            }
        }

        if (counters.RowsDeduplicated > 0)
        {
            Console.WriteLine(
                $"[warn] Deduplicated {counters.RowsDeduplicated} result rows with repeated source_record_key values; " +
                $"{counters.RowsConflictingDuplicates} collisions had different row contents.");
        }

        return resultsByKey.Values.ToArray();
    }

    // ============================================================
    // Supabase: import runs + errors
    // ============================================================
    private static async Task<string> StartImportRunAsync(
        SupabaseRestClient supa,
        string sourceId,
        ImporterOptions opts,
        CancellationToken ct)
    {
        var row = new JsonArray
        {
            new JsonObject
            {
                ["source_id"] = sourceId,
                ["status"] = "running",
                ["source_url"] = opts.CsvUrl ?? DefaultCsvUrl(opts.Source),
                ["triggered_by"] = opts.TriggeredBy,
                ["notes"] = new JsonObject
                {
                    ["competition_json"] = opts.CompetitionJsonPath
                }
            }
        };
        var returned = await supa.UpsertReturningAsync("public", "import_runs", "id", row, ct);
        return returned[0]!["id"]!.ToString();
    }

    private static async Task FinishImportRunAsync(
        SupabaseRestClient supa,
        string runId,
        string status,
        ImportCounters c,
        ImporterOptions opts,
        string? errorMessage,
        CancellationToken ct)
    {
        var patch = new JsonObject
        {
            ["status"] = status,
            ["finished_at"] = DateTimeOffset.UtcNow.ToString("o"),
            ["rows_processed"] = c.RowsProcessed,
            ["rows_inserted"] = c.RowsInserted,
            ["rows_updated"] = c.RowsUpdated,
            ["rows_skipped"] = c.RowsSkipped,
            ["rows_failed"] = c.RowsFailed,
            ["error_message"] = errorMessage,
            ["notes"] = new JsonObject
            {
                ["competition_json"] = opts.CompetitionJsonPath,
                ["deduplicated_rows"] = c.RowsDeduplicated,
                ["conflicting_duplicate_rows"] = c.RowsConflictingDuplicates
            }
        };
        await supa.PatchAsync("public", "import_runs", $"id=eq.{runId}", patch, ct);
    }

    private static async Task WriteErrorsAsync(
        SupabaseRestClient supa,
        IReadOnlyList<JsonObject> errors,
        int batchSize,
        CancellationToken ct)
    {
        foreach (var chunk in errors.Chunk(batchSize))
        {
            var arr = new JsonArray();
            foreach (var e in chunk) arr.Add(e.DeepClone());
            await supa.UpsertAsync("public", "import_errors", "id", arr, ct);
        }
    }
}
