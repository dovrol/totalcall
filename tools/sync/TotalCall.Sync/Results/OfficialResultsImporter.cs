using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using TotalCall.Client.Domain.Competitions;
using TotalCall.Client.Domain.Predictions;
using TotalCall.Client.Scoring;
using TotalCall.Sync.Competitions;

namespace TotalCall.Sync.Results;

public sealed class ResultsImportOptions
{
    public required string CompetitionId { get; init; }
    public required string ResultsJsonPath { get; init; }
    public string? SupabaseUrl { get; init; }
    public string? SupabaseSecretKey { get; init; }
    public string TriggeredBy { get; init; } = "manual";
}

public sealed class OfficialResultsImporter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IPredictionScoringService scoringService = new PredictionScoringService(
    [
        new AthleteRankingQuestionScorer(),
        new CategoryPodiumQuestionScorer()
    ]);

    public async Task<int> RunAsync(ResultsImportOptions opts, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(opts.CompetitionId))
        {
            Console.Error.WriteLine("[error] results: --competition-id is required.");
            return 1;
        }

        if (string.IsNullOrWhiteSpace(opts.ResultsJsonPath))
        {
            Console.Error.WriteLine("[error] results: --results-json is required.");
            return 1;
        }

        if (!File.Exists(opts.ResultsJsonPath))
        {
            Console.Error.WriteLine($"[error] Results JSON not found: {opts.ResultsJsonPath}");
            return 1;
        }

        if (string.IsNullOrWhiteSpace(opts.SupabaseUrl) || string.IsNullOrWhiteSpace(opts.SupabaseSecretKey))
        {
            Console.Error.WriteLine("[error] SUPABASE_URL and SUPABASE_SECRET_KEY must be set.");
            return 2;
        }

        Console.WriteLine($"[info] Loading official results: {opts.ResultsJsonPath}");
        var rawJson = await File.ReadAllTextAsync(opts.ResultsJsonPath, ct);
        var resultsNode = JsonNode.Parse(rawJson) as JsonObject;
        if (resultsNode is null)
        {
            Console.Error.WriteLine("[error] Results JSON root must be an object.");
            return 1;
        }

        var resultsFile = resultsNode.Deserialize<OfficialResultsFile>(JsonOptions);
        if (resultsFile is null)
        {
            Console.Error.WriteLine("[error] Could not parse results JSON.");
            return 1;
        }

        var supabase = new SupabaseRestClient(opts.SupabaseUrl, opts.SupabaseSecretKey);
        var published = await LoadPublishedCompetitionAsync(supabase, opts.CompetitionId, ct);
        if (published is null)
        {
            return 3;
        }

        var validationErrors = new OfficialResultsValidator().Validate(
            published.Competition,
            resultsFile,
            opts.CompetitionId);
        if (validationErrors.Count > 0)
        {
            foreach (var error in validationErrors)
            {
                Console.Error.WriteLine($"[error] {error}");
            }

            return 3;
        }

        var incomingHash = CompetitionConfigHasher.Compute(resultsNode);
        var officialResultId = await UpsertOfficialResultsAsync(
            supabase,
            opts.CompetitionId,
            NormalizeImportStatus(resultsFile.Status),
            resultsFile.Source ?? opts.TriggeredBy,
            incomingHash,
            ct);

        await UpsertOfficialResultGroupsAsync(
            supabase,
            officialResultId,
            published.Competition,
            resultsFile,
            ct);

        var officialResults = await LoadOfficialResultsAsync(supabase, opts.CompetitionId, ct);
        var submissions = await LoadSubmissionsAsync(supabase, opts.CompetitionId, ct);
        var competitionByVersionId = await LoadSubmissionCompetitionVersionsAsync(
            supabase,
            published,
            submissions,
            ct);

        var calculatedAt = DateTimeOffset.UtcNow;
        var snapshotBuilder = new ScoreSnapshotBuilder(scoringService);
        var scoreRows = snapshotBuilder.Build(
            submissions,
            competitionByVersionId,
            officialResults,
            calculatedAt);

        var summary = scoringService.Score(
            published.Competition,
            CreateEmptySubmittedPredictionSet(published.Competition),
            officialResults);

        if (summary.ScoredGroupsCount == 0 || scoreRows.Count == 0)
        {
            await supabase.DeleteAsync(
                "public",
                "score_snapshots",
                $"competition_id=eq.{Uri.EscapeDataString(opts.CompetitionId)}",
                ct);
        }
        else
        {
            var snapshotRows = new JsonArray();
            foreach (var row in scoreRows)
            {
                snapshotRows.Add(row.ToJsonObject());
            }

            await supabase.UpsertAsync(
                "public",
                "score_snapshots",
                "competition_id,user_id",
                snapshotRows,
                ct);
        }

        var pendingGroupsCount = Math.Max(0, summary.TotalGroupsCount - summary.ScoredGroupsCount);
        Console.WriteLine($"[report] final groups: {summary.ScoredGroupsCount}");
        Console.WriteLine($"[report] pending groups: {pendingGroupsCount}");
        Console.WriteLine($"[report] leaderboard status: {summary.Status.ToString().ToLowerInvariant()}");
        Console.WriteLine($"[report] submitted rows scored: {scoreRows.Count}");
        Console.WriteLine($"[report] last calculated at: {calculatedAt:O}");
        Console.WriteLine("[done] Imported official results and recalculated score snapshots.");

        return 0;
    }

    private static async Task<PublishedCompetition?> LoadPublishedCompetitionAsync(
        SupabaseRestClient supabase,
        string competitionId,
        CancellationToken ct)
    {
        var competitionRows = await supabase.GetAsync(
            "public",
            "competitions",
            $"id=eq.{Uri.EscapeDataString(competitionId)}&select=id,published_version_id",
            ct);
        var competitionRow = competitionRows.OfType<JsonObject>().FirstOrDefault();
        if (competitionRow is null)
        {
            Console.Error.WriteLine($"[error] Competition '{competitionId}' does not exist in Supabase.");
            return null;
        }

        var versionId = competitionRow["published_version_id"]?.ToString();
        if (string.IsNullOrWhiteSpace(versionId))
        {
            Console.Error.WriteLine($"[error] Competition '{competitionId}' has no published config version.");
            return null;
        }

        var version = await LoadCompetitionVersionAsync(supabase, versionId, ct);
        if (version is null)
        {
            Console.Error.WriteLine($"[error] Published competition_version '{versionId}' was not found.");
            return null;
        }

        return version;
    }

    private static async Task<PublishedCompetition?> LoadCompetitionVersionAsync(
        SupabaseRestClient supabase,
        string versionId,
        CancellationToken ct)
    {
        var versionRows = await supabase.GetAsync(
            "public",
            "competition_versions",
            $"id=eq.{Uri.EscapeDataString(versionId)}&select=id,version,config",
            ct);
        var versionRow = versionRows.OfType<JsonObject>().FirstOrDefault();
        if (versionRow is null)
        {
            return null;
        }

        var config = versionRow["config"]?.Deserialize<Competition>(JsonOptions);
        if (config is null)
        {
            Console.Error.WriteLine($"[error] competition_version '{versionId}' has invalid config JSON.");
            return null;
        }

        return new PublishedCompetition(versionId, config);
    }

    private static async Task<string> UpsertOfficialResultsAsync(
        SupabaseRestClient supabase,
        string competitionId,
        string status,
        string? source,
        string resultsHash,
        CancellationToken ct)
    {
        var rows = await supabase.UpsertReturningAsync(
            "public",
            "official_results",
            "competition_id",
            new JsonArray
            {
                new JsonObject
                {
                    ["competition_id"] = competitionId,
                    ["status"] = status,
                    ["source"] = source,
                    ["results_hash"] = resultsHash,
                    ["imported_at"] = DateTimeOffset.UtcNow.ToString("o")
                }
            },
            ct);

        var id = rows.OfType<JsonObject>().FirstOrDefault()?["id"]?.ToString();
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new InvalidOperationException("official_results upsert returned no id.");
        }

        return id;
    }

    private static async Task UpsertOfficialResultGroupsAsync(
        SupabaseRestClient supabase,
        string officialResultId,
        Competition competition,
        OfficialResultsFile resultsFile,
        CancellationToken ct)
    {
        if (resultsFile.Groups.Count == 0)
        {
            return;
        }

        var rows = new JsonArray();
        foreach (var group in resultsFile.Groups)
        {
            var question = FindQuestion(competition, group.GroupId, group.QuestionId)
                ?? throw new InvalidOperationException(
                    $"Validated group '{group.GroupId}/{group.QuestionId}' could not be resolved.");
            var categoryId = string.IsNullOrWhiteSpace(group.CategoryId)
                ? question.CategoryId
                : group.CategoryId;
            var status = NormalizeGroupStatus(group.Status);
            var resultJson = BuildResultJson(group, categoryId, status);

            rows.Add(new JsonObject
            {
                ["official_result_id"] = officialResultId,
                ["competition_id"] = resultsFile.CompetitionId,
                ["group_id"] = group.GroupId,
                ["question_id"] = group.QuestionId,
                ["category_id"] = categoryId,
                ["status"] = status,
                ["result_json"] = JsonNode.Parse(resultJson.ToJsonString())!,
                ["result_hash"] = CompetitionConfigHasher.Compute(resultJson),
                ["imported_at"] = DateTimeOffset.UtcNow.ToString("o"),
                ["finalized_at"] = string.Equals(status, OfficialResultGroupImportStatus.Final, StringComparison.Ordinal)
                    ? DateTimeOffset.UtcNow.ToString("o")
                    : null
            });
        }

        await supabase.UpsertAsync(
            "public",
            "official_result_groups",
            "competition_id,group_id,question_id",
            rows,
            ct);
    }

    private static async Task<OfficialCompetitionResults> LoadOfficialResultsAsync(
        SupabaseRestClient supabase,
        string competitionId,
        CancellationToken ct)
    {
        var rows = await supabase.GetAsync(
            "public",
            "official_result_groups",
            $"competition_id=eq.{Uri.EscapeDataString(competitionId)}&select=group_id,question_id,category_id,status,result_json,result_hash",
            ct);

        var groups = rows
            .OfType<JsonObject>()
            .Select(ParseOfficialResultGroup)
            .OrderBy(group => group.GroupId, StringComparer.OrdinalIgnoreCase)
            .ThenBy(group => group.QuestionId, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var aggregateGroups = new JsonArray();
        foreach (var group in groups)
        {
            aggregateGroups.Add(BuildResultJson(group));
        }

        var aggregate = new JsonObject
        {
            ["competitionId"] = competitionId,
            ["groups"] = aggregateGroups
        };

        return new OfficialCompetitionResults
        {
            CompetitionId = competitionId,
            ResultsHash = CompetitionConfigHasher.Compute(aggregate),
            Groups = groups
        };
    }

    private static async Task<IReadOnlyList<PredictionSubmissionImportRow>> LoadSubmissionsAsync(
        SupabaseRestClient supabase,
        string competitionId,
        CancellationToken ct)
    {
        var rows = await supabase.GetAsync(
            "public",
            "prediction_submissions",
            $"competition_id=eq.{Uri.EscapeDataString(competitionId)}&select=id,user_id,competition_id,competition_version_id,status,submitted_at,answers_json",
            ct);

        var submissions = new List<PredictionSubmissionImportRow>();
        foreach (var row in rows.OfType<JsonObject>())
        {
            var predictionSet = row["answers_json"]?.Deserialize<PredictionSet>(JsonOptions);
            if (predictionSet is null)
            {
                Console.WriteLine($"[warn] Skipping submission '{row["id"]}' because answers_json is invalid.");
                continue;
            }

            submissions.Add(new PredictionSubmissionImportRow(
                RequiredString(row, "id"),
                RequiredString(row, "user_id"),
                RequiredString(row, "competition_id"),
                row["competition_version_id"]?.ToString(),
                row["status"]?.ToString(),
                ParseDateTimeOffset(row["submitted_at"]),
                predictionSet));
        }

        return submissions;
    }

    private static async Task<IReadOnlyDictionary<string, Competition>> LoadSubmissionCompetitionVersionsAsync(
        SupabaseRestClient supabase,
        PublishedCompetition published,
        IReadOnlyList<PredictionSubmissionImportRow> submissions,
        CancellationToken ct)
    {
        var result = new Dictionary<string, Competition>(StringComparer.OrdinalIgnoreCase)
        {
            [published.VersionId] = published.Competition
        };

        var versionIds = submissions
            .Where(submission => submission.IsSubmitted)
            .Select(submission => submission.CompetitionVersionId)
            .Where(versionId => !string.IsNullOrWhiteSpace(versionId))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        foreach (var versionId in versionIds)
        {
            if (result.ContainsKey(versionId!))
            {
                continue;
            }

            var version = await LoadCompetitionVersionAsync(supabase, versionId!, ct);
            if (version is null)
            {
                Console.WriteLine($"[warn] Skipping submissions for missing competition_version_id '{versionId}'.");
                continue;
            }

            result[version.VersionId] = version.Competition;
        }

        return result;
    }

    private static OfficialResultGroup ParseOfficialResultGroup(JsonObject row)
    {
        var resultJson = row["result_json"] as JsonObject ?? new JsonObject();
        var placements = (resultJson["placements"] as JsonArray)?
            .OfType<JsonObject>()
            .Select(placement => new OfficialAthleteResult
            {
                Position = GetInt(placement, "position"),
                AthleteId = RequiredString(placement, "athleteId"),
                SquatKg = GetDecimal(placement, "squatKg"),
                BenchKg = GetDecimal(placement, "benchKg"),
                DeadliftKg = GetDecimal(placement, "deadliftKg"),
                TotalKg = GetDecimal(placement, "totalKg")
            })
            .ToArray() ?? [];

        return new OfficialResultGroup
        {
            GroupId = RequiredString(row, "group_id"),
            QuestionId = RequiredString(row, "question_id"),
            CategoryId = row["category_id"]?.ToString(),
            Status = string.Equals(row["status"]?.ToString(), OfficialResultGroupImportStatus.Final, StringComparison.OrdinalIgnoreCase)
                ? OfficialResultGroupStatus.Final
                : OfficialResultGroupStatus.Pending,
            ResultHash = row["result_hash"]?.ToString(),
            Placements = placements
        };
    }

    private static JsonObject BuildResultJson(
        OfficialResultGroupFile group,
        string? categoryId,
        string status)
    {
        var placements = new JsonArray();
        foreach (var placement in group.Placements.OrderBy(placement => placement.Position))
        {
            placements.Add(new JsonObject
            {
                ["position"] = placement.Position,
                ["athleteId"] = placement.AthleteId,
                ["squatKg"] = placement.SquatKg,
                ["benchKg"] = placement.BenchKg,
                ["deadliftKg"] = placement.DeadliftKg,
                ["totalKg"] = placement.TotalKg
            });
        }

        return new JsonObject
        {
            ["groupId"] = group.GroupId,
            ["questionId"] = group.QuestionId,
            ["categoryId"] = categoryId,
            ["status"] = status,
            ["placements"] = placements
        };
    }

    private static JsonObject BuildResultJson(OfficialResultGroup group)
    {
        var placements = new JsonArray();
        foreach (var placement in group.Placements.OrderBy(placement => placement.Position))
        {
            placements.Add(new JsonObject
            {
                ["position"] = placement.Position,
                ["athleteId"] = placement.AthleteId,
                ["squatKg"] = placement.SquatKg,
                ["benchKg"] = placement.BenchKg,
                ["deadliftKg"] = placement.DeadliftKg,
                ["totalKg"] = placement.TotalKg
            });
        }

        return new JsonObject
        {
            ["groupId"] = group.GroupId,
            ["questionId"] = group.QuestionId,
            ["categoryId"] = group.CategoryId,
            ["status"] = group.Status.ToString().ToLowerInvariant(),
            ["resultHash"] = group.ResultHash,
            ["placements"] = placements
        };
    }

    private static PredictionQuestion? FindQuestion(
        Competition competition,
        string groupId,
        string questionId)
    {
        var group = competition.PredictionGroups.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, groupId, StringComparison.OrdinalIgnoreCase));

        return group?.Questions.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, questionId, StringComparison.OrdinalIgnoreCase));
    }

    private static PredictionSet CreateEmptySubmittedPredictionSet(Competition competition)
    {
        return new PredictionSet
        {
            CompetitionId = competition.Id,
            CompetitionConfigVersion = competition.ConfigVersion,
            SubmissionStatus = PredictionSet.SubmittedSubmissionStatus,
            SubmittedAt = DateTimeOffset.UtcNow
        };
    }

    private static string NormalizeImportStatus(string status) =>
        string.Equals(status, OfficialResultImportStatus.Final, StringComparison.OrdinalIgnoreCase)
            ? OfficialResultImportStatus.Final
            : OfficialResultImportStatus.Partial;

    private static string NormalizeGroupStatus(string status) =>
        string.Equals(status, OfficialResultGroupImportStatus.Final, StringComparison.OrdinalIgnoreCase)
            ? OfficialResultGroupImportStatus.Final
            : OfficialResultGroupImportStatus.Pending;

    private static string RequiredString(JsonObject obj, string propertyName)
    {
        var value = obj[propertyName]?.ToString();
        return string.IsNullOrWhiteSpace(value)
            ? throw new InvalidOperationException($"JSON object is missing '{propertyName}'.")
            : value;
    }

    private static int GetInt(JsonObject obj, string propertyName)
    {
        var value = obj[propertyName]?.ToString();
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : 0;
    }

    private static decimal? GetDecimal(JsonObject obj, string propertyName)
    {
        var value = obj[propertyName]?.ToString();
        return decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    private static DateTimeOffset? ParseDateTimeOffset(JsonNode? node)
    {
        var value = node?.ToString();
        return DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed)
            ? parsed
            : null;
    }

    private sealed record PublishedCompetition(string VersionId, Competition Competition);
}
