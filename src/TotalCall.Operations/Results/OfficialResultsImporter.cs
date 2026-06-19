using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using TotalCall.Core.Domain.Competitions;
using TotalCall.Core.Domain.Predictions;
using TotalCall.Core.Scoring;
using TotalCall.Operations;
using TotalCall.Operations.Competitions;
using TotalCall.Operations.Supabase;

namespace TotalCall.Operations.Results;

public sealed class ResultsImportOptions
{
    public required string CompetitionId { get; init; }
    public required string ResultsJsonPath { get; init; }
    public string? SupabaseUrl { get; init; }
    public string? SupabaseSecretKey { get; init; }
    public string TriggeredBy { get; init; } = "manual";
}

public sealed record ResultsImportResult(
    int ExitCode,
    string? CompetitionId,
    string? ResultsJsonPath,
    string? Status,
    string? Source,
    string? ResultsHash,
    int GroupsInFile,
    int GroupsImported,
    int SubmittedRowsScored,
    int FinalGroupsCount,
    int PendingGroupsCount,
    string? LeaderboardStatus,
    DateTimeOffset? CalculatedAt,
    bool ScoreSnapshotsDeleted,
    IReadOnlyList<OperationLogEntry> Logs)
{
    public bool Succeeded => ExitCode == 0;
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
        var result = await ImportAsync(opts, ct);
        WriteLogs(result.Logs);
        return result.ExitCode;
    }

    public async Task<ResultsImportResult> ImportAsync(ResultsImportOptions opts, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(opts.CompetitionId))
        {
            var logs = new List<OperationLogEntry>
            {
                OperationLogEntry.Error("results: --competition-id is required.")
            };
            return Finish(1, logs);
        }

        var inputResultsJsonPath = opts.ResultsJsonPath?.Trim();
        var operationLogs = new List<OperationLogEntry>
        {
            OperationLogEntry.Info($"Loading official results: {inputResultsJsonPath}")
        };

        var competitionId = opts.CompetitionId.Trim();

        if (string.IsNullOrWhiteSpace(inputResultsJsonPath))
        {
            operationLogs.Add(OperationLogEntry.Error("results: --results-json is required."));
            return Finish(1, operationLogs, competitionId);
        }

        var resultsJsonPath = ResolveFullPath(inputResultsJsonPath);
        if (!File.Exists(resultsJsonPath))
        {
            operationLogs.Add(OperationLogEntry.Error($"Results JSON not found: {resultsJsonPath}"));
            return Finish(1, operationLogs, competitionId, resultsJsonPath);
        }

        string rawJson;
        try
        {
            rawJson = await File.ReadAllTextAsync(resultsJsonPath, ct);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            operationLogs.Add(OperationLogEntry.Error($"Results JSON could not be read: {ex.Message}"));
            return Finish(1, operationLogs, competitionId, resultsJsonPath);
        }

        JsonObject resultsNode;
        try
        {
            if (JsonNode.Parse(rawJson) is not JsonObject parsed)
            {
                operationLogs.Add(OperationLogEntry.Error("Results JSON root must be an object."));
                return Finish(1, operationLogs, competitionId, resultsJsonPath);
            }

            resultsNode = parsed;
        }
        catch (JsonException ex)
        {
            operationLogs.Add(OperationLogEntry.Error($"Results JSON could not be parsed: {ex.Message}"));
            return Finish(1, operationLogs, competitionId, resultsJsonPath);
        }

        OfficialResultsFile? resultsFile;
        try
        {
            resultsFile = resultsNode.Deserialize<OfficialResultsFile>(JsonOptions);
        }
        catch (JsonException ex)
        {
            operationLogs.Add(OperationLogEntry.Error($"Could not parse results JSON: {ex.Message}"));
            return Finish(1, operationLogs, competitionId, resultsJsonPath);
        }

        if (resultsFile is null)
        {
            operationLogs.Add(OperationLogEntry.Error("Could not parse results JSON."));
            return Finish(1, operationLogs, competitionId, resultsJsonPath);
        }

        if (string.IsNullOrWhiteSpace(resultsFile.CompetitionId))
        {
            operationLogs.Add(OperationLogEntry.Error("results.competitionId is required."));
            return Finish(
                1,
                operationLogs,
                competitionId,
                resultsJsonPath,
                resultsFile);
        }

        if (!string.Equals(resultsFile.CompetitionId, competitionId, StringComparison.OrdinalIgnoreCase))
        {
            operationLogs.Add(OperationLogEntry.Error(
                $"results.competitionId '{resultsFile.CompetitionId}' does not match '{competitionId}'."));
            return Finish(
                1,
                operationLogs,
                competitionId,
                resultsJsonPath,
                resultsFile);
        }

        var shapeErrors = ValidateImportShape(resultsFile);
        if (shapeErrors.Count > 0)
        {
            foreach (var error in shapeErrors)
            {
                operationLogs.Add(OperationLogEntry.Error(error));
            }

            return Finish(
                1,
                operationLogs,
                competitionId,
                resultsJsonPath,
                resultsFile);
        }

        var incomingHash = CompetitionConfigHasher.Compute(resultsNode);

        if (string.IsNullOrWhiteSpace(opts.SupabaseUrl) || string.IsNullOrWhiteSpace(opts.SupabaseSecretKey))
        {
            operationLogs.Add(OperationLogEntry.Error("SUPABASE_URL and SUPABASE_SECRET_KEY must be set."));
            return Finish(
                2,
                operationLogs,
                competitionId,
                resultsJsonPath,
                resultsFile,
                incomingHash);
        }

        var supabase = new SupabaseRestClient(opts.SupabaseUrl, opts.SupabaseSecretKey);
        var published = await LoadPublishedCompetitionAsync(supabase, competitionId, operationLogs, ct);
        if (published is null)
        {
            return Finish(
                3,
                operationLogs,
                competitionId,
                resultsJsonPath,
                resultsFile,
                incomingHash);
        }

        var validationErrors = new OfficialResultsValidator().Validate(
            published.Competition,
            resultsFile,
            competitionId);
        if (validationErrors.Count > 0)
        {
            foreach (var error in validationErrors)
            {
                operationLogs.Add(OperationLogEntry.Error(error));
            }

            return Finish(
                3,
                operationLogs,
                competitionId,
                resultsJsonPath,
                resultsFile,
                incomingHash);
        }

        var officialResultId = await UpsertOfficialResultsAsync(
            supabase,
            competitionId,
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
        operationLogs.Add(OperationLogEntry.Info($"Imported {resultsFile.Groups.Count} official result groups."));

        var officialResults = await LoadOfficialResultsAsync(supabase, competitionId, ct);
        var submissions = await LoadSubmissionsAsync(supabase, competitionId, operationLogs, ct);
        var competitionByVersionId = await LoadSubmissionCompetitionVersionsAsync(
            supabase,
            published,
            submissions,
            operationLogs,
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
                $"competition_id=eq.{Uri.EscapeDataString(competitionId)}",
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
        var leaderboardStatus = summary.Status.ToString().ToLowerInvariant();
        var snapshotsDeleted = summary.ScoredGroupsCount == 0 || scoreRows.Count == 0;
        operationLogs.Add(OperationLogEntry.Info($"final groups: {summary.ScoredGroupsCount}"));
        operationLogs.Add(OperationLogEntry.Info($"pending groups: {pendingGroupsCount}"));
        operationLogs.Add(OperationLogEntry.Info($"leaderboard status: {leaderboardStatus}"));
        operationLogs.Add(OperationLogEntry.Info($"submitted rows scored: {scoreRows.Count}"));
        operationLogs.Add(OperationLogEntry.Info($"last calculated at: {calculatedAt:O}"));
        operationLogs.Add(OperationLogEntry.Done("Imported official results and recalculated score snapshots."));

        return Finish(
            0,
            operationLogs,
            competitionId,
            resultsJsonPath,
            resultsFile,
            incomingHash,
            resultsFile.Groups.Count,
            scoreRows.Count,
            summary.ScoredGroupsCount,
            pendingGroupsCount,
            leaderboardStatus,
            calculatedAt,
            snapshotsDeleted);
    }

    private static async Task<PublishedCompetition?> LoadPublishedCompetitionAsync(
        SupabaseRestClient supabase,
        string competitionId,
        ICollection<OperationLogEntry> logs,
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
            logs.Add(OperationLogEntry.Error($"Competition '{competitionId}' does not exist in Supabase."));
            return null;
        }

        var versionId = competitionRow["published_version_id"]?.ToString();
        if (string.IsNullOrWhiteSpace(versionId))
        {
            logs.Add(OperationLogEntry.Error($"Competition '{competitionId}' has no published config version."));
            return null;
        }

        var version = await LoadCompetitionVersionAsync(supabase, versionId, logs, ct);
        if (version is null)
        {
            logs.Add(OperationLogEntry.Error($"Published competition_version '{versionId}' was not found."));
            return null;
        }

        return version;
    }

    private static async Task<PublishedCompetition?> LoadCompetitionVersionAsync(
        SupabaseRestClient supabase,
        string versionId,
        ICollection<OperationLogEntry> logs,
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
            logs.Add(OperationLogEntry.Error($"competition_version '{versionId}' has invalid config JSON."));
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
        ICollection<OperationLogEntry> logs,
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
                logs.Add(OperationLogEntry.Warn($"Skipping submission '{row["id"]}' because answers_json is invalid."));
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
        ICollection<OperationLogEntry> logs,
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

            var version = await LoadCompetitionVersionAsync(supabase, versionId!, logs, ct);
            if (version is null)
            {
                logs.Add(OperationLogEntry.Warn($"Skipping submissions for missing competition_version_id '{versionId}'."));
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

    private static IReadOnlyList<string> ValidateImportShape(OfficialResultsFile resultsFile)
    {
        var errors = new List<string>();
        if (!IsImportStatus(resultsFile.Status))
        {
            errors.Add("results.status must be 'partial' or 'final'.");
        }

        foreach (var group in resultsFile.Groups)
        {
            if (!IsGroupImportStatus(group.Status))
            {
                errors.Add(
                    $"results.groups[{group.GroupId}/{group.QuestionId}].status must be 'pending' or 'final'.");
            }

            if (string.Equals(group.Status, OfficialResultGroupImportStatus.Final, StringComparison.OrdinalIgnoreCase) &&
                group.Placements.Count == 0)
            {
                errors.Add($"Final result group '{group.GroupId}/{group.QuestionId}' must include placements.");
            }
        }

        return errors;
    }

    private static bool IsImportStatus(string? status) =>
        string.Equals(status, OfficialResultImportStatus.Partial, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(status, OfficialResultImportStatus.Final, StringComparison.OrdinalIgnoreCase);

    private static bool IsGroupImportStatus(string? status) =>
        string.Equals(status, OfficialResultGroupImportStatus.Pending, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(status, OfficialResultGroupImportStatus.Final, StringComparison.OrdinalIgnoreCase);

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

    private static string ResolveFullPath(string path)
    {
        if (Path.IsPathRooted(path))
        {
            return Path.GetFullPath(path);
        }

        var currentDirectoryPath = Path.GetFullPath(path);
        if (File.Exists(currentDirectoryPath))
        {
            return currentDirectoryPath;
        }

        var repositoryRoot = FindRepositoryRoot(Directory.GetCurrentDirectory())
            ?? FindRepositoryRoot(AppContext.BaseDirectory);
        if (repositoryRoot is not null)
        {
            var repositoryPath = Path.GetFullPath(Path.Combine(repositoryRoot, path));
            if (File.Exists(repositoryPath) || IsRepositoryRelativePath(path))
            {
                return repositoryPath;
            }
        }

        return currentDirectoryPath;
    }

    private static bool IsRepositoryRelativePath(string path) =>
        path.StartsWith("src/", StringComparison.Ordinal)
        || path.StartsWith("src\\", StringComparison.Ordinal)
        || path.StartsWith("tools/", StringComparison.Ordinal)
        || path.StartsWith("tools\\", StringComparison.Ordinal)
        || path.StartsWith("supabase/", StringComparison.Ordinal)
        || path.StartsWith("supabase\\", StringComparison.Ordinal);

    private static string? FindRepositoryRoot(string startPath)
    {
        var directory = new DirectoryInfo(startPath);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "TotalCall.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return null;
    }

    private static ResultsImportResult Finish(
        int exitCode,
        List<OperationLogEntry> logs,
        string? competitionId = null,
        string? resultsJsonPath = null,
        OfficialResultsFile? resultsFile = null,
        string? resultsHash = null,
        int? groupsImported = null,
        int submittedRowsScored = 0,
        int finalGroupsCount = 0,
        int pendingGroupsCount = 0,
        string? leaderboardStatus = null,
        DateTimeOffset? calculatedAt = null,
        bool scoreSnapshotsDeleted = false) => new(
            exitCode,
            competitionId,
            resultsJsonPath,
            resultsFile?.Status,
            resultsFile?.Source,
            resultsHash,
            resultsFile?.Groups.Count ?? 0,
            groupsImported ?? 0,
            submittedRowsScored,
            finalGroupsCount,
            pendingGroupsCount,
            leaderboardStatus,
            calculatedAt,
            scoreSnapshotsDeleted,
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

    private sealed record PublishedCompetition(string VersionId, Competition Competition);
}
