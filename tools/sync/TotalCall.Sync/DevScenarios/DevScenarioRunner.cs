using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using TotalCall.Client.Domain.Competitions;
using TotalCall.Client.Domain.Predictions;
using TotalCall.Sync.Results;

namespace TotalCall.Sync.DevScenarios;

public sealed class DevScenarioOptions
{
    public required string ScenarioName { get; init; }
    public required bool Local { get; init; }
    public string? SupabaseUrl { get; init; }
    public string? SupabaseSecretKey { get; init; }
    public string? BaseCompetitionJsonPath { get; init; }
    public string TriggeredBy { get; init; } = "dev-scenario";
}

public sealed class DevScenarioRunner
{
    public const string AllStatesScenarioName = "all-states";

    private const string DevConfigVersion = "dev-scenarios-v1";
    private const string DevAppVersion = "dev-scenarios-v1";
    private const string DefaultBaseCompetitionJsonPath =
        "src/TotalCall.Client/wwwroot/data/competitions/worlds-2026.json";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly DateTimeOffset FutureOpenAt = DateTimeOffset.Parse("2099-01-01T00:00:00Z");
    private static readonly DateTimeOffset FutureStartAt = DateTimeOffset.Parse("2099-06-13T07:00:00Z");
    private static readonly DateTimeOffset FutureLockAt = DateTimeOffset.Parse("2099-06-13T06:00:00Z");
    private static readonly DateTimeOffset FutureEndAt = DateTimeOffset.Parse("2099-06-21T14:00:00Z");
    private static readonly DateTimeOffset PastOpenAt = DateTimeOffset.Parse("2020-01-01T00:00:00Z");
    private static readonly DateTimeOffset PastStartAt = DateTimeOffset.Parse("2020-06-13T07:00:00Z");
    private static readonly DateTimeOffset PastLockAt = DateTimeOffset.Parse("2020-06-13T06:00:00Z");
    private static readonly DateTimeOffset PastEndAt = DateTimeOffset.Parse("2020-06-21T14:00:00Z");
    private static readonly DateTimeOffset SubmittedAt = DateTimeOffset.Parse("2026-01-15T12:00:00Z");

    private static readonly DevScenarioUser[] ScenarioUsers =
    [
        new("dev-alice@totalcall.local", "Dev Alice"),
        new("dev-bruno@totalcall.local", "Dev Bruno"),
        new("dev-casey@totalcall.local", "Dev Casey")
    ];

    private static readonly DevCompetitionScenario[] Scenarios =
    [
        new(
            "dev-open",
            ["open"],
            "Dev Open",
            CompetitionStatus.Upcoming,
            FutureOpenAt,
            FutureStartAt,
            FutureLockAt,
            FutureEndAt,
            SubmittedUsersCount: 0,
            ResultsMode.None),
        new(
            "dev-open-with-submissions",
            ["open-with-submissions"],
            "Dev Open With Submissions",
            CompetitionStatus.Upcoming,
            FutureOpenAt,
            FutureStartAt.AddDays(1),
            FutureLockAt.AddDays(1),
            FutureEndAt.AddDays(1),
            SubmittedUsersCount: 3,
            ResultsMode.None),
        new(
            "dev-locked-no-results",
            ["locked-no-results"],
            "Dev Locked No Results",
            CompetitionStatus.Locked,
            PastOpenAt,
            FutureStartAt.AddDays(2),
            PastLockAt,
            FutureEndAt.AddDays(2),
            SubmittedUsersCount: 3,
            ResultsMode.None),
        new(
            "dev-partial-results",
            ["partial-results"],
            "Dev Partial Results",
            CompetitionStatus.Locked,
            PastOpenAt,
            FutureStartAt.AddDays(3),
            PastLockAt,
            FutureEndAt.AddDays(3),
            SubmittedUsersCount: 3,
            new ResultsMode("partial", 3)),
        new(
            "dev-final-results",
            ["final-results"],
            "Dev Final Results",
            CompetitionStatus.Completed,
            PastOpenAt,
            PastStartAt,
            PastLockAt,
            PastEndAt,
            SubmittedUsersCount: 3,
            new ResultsMode("final", null)),
        new(
            "dev-empty",
            ["empty"],
            "Dev Empty",
            CompetitionStatus.Upcoming,
            FutureOpenAt,
            FutureStartAt.AddDays(4),
            FutureLockAt.AddDays(4),
            FutureEndAt.AddDays(4),
            SubmittedUsersCount: 0,
            ResultsMode.None)
    ];

    public async Task<int> RunAsync(DevScenarioOptions opts, CancellationToken ct)
    {
        var guard = ValidateDevGuard(opts);
        if (guard is not null)
        {
            Console.Error.WriteLine($"[guard] {guard}");
            return 2;
        }

        var selectedScenarios = ResolveScenarios(opts.ScenarioName);
        if (selectedScenarios.Count == 0)
        {
            Console.Error.WriteLine($"[error] Unknown scenario: {opts.ScenarioName}");
            PrintScenarioList();
            return 1;
        }

        var basePath = opts.BaseCompetitionJsonPath ?? DefaultBaseCompetitionJsonPath;
        if (!File.Exists(basePath))
        {
            Console.Error.WriteLine($"[error] Base competition JSON not found: {basePath}");
            return 1;
        }

        var baseCompetition = await LoadCompetitionAsync(basePath, ct);
        if (baseCompetition is null)
        {
            return 1;
        }

        var supabase = new SupabaseRestClient(opts.SupabaseUrl!, opts.SupabaseSecretKey!);
        var auth = new SupabaseAuthAdminClient(opts.SupabaseUrl!, opts.SupabaseSecretKey!);

        Console.WriteLine($"[info] Dev scenario target: {opts.SupabaseUrl}");
        Console.WriteLine($"[info] Scenario: {opts.ScenarioName}");
        Console.WriteLine("[info] Resetting selected dev scenario competitions...");
        await ResetScenarioDataAsync(supabase, selectedScenarios.Select(scenario => scenario.CompetitionId), ct);

        var users = await EnsureScenarioUsersAsync(auth, supabase, ct);

        foreach (var scenario in selectedScenarios)
        {
            Console.WriteLine($"[scenario] {scenario.CompetitionId}");
            var competition = CreateCompetition(baseCompetition, scenario);
            await PublishCompetitionAsync(supabase, competition, scenario, ct);

            if (scenario.SubmittedUsersCount > 0)
            {
                await SeedSubmittedPredictionsAsync(
                    supabase,
                    competition,
                    users.Take(scenario.SubmittedUsersCount).ToArray(),
                    ct);
            }

            if (scenario.Results.HasResults)
            {
                var code = await ImportResultsAsync(scenario, competition, opts, ct);
                if (code != 0)
                {
                    return code;
                }
            }

            Console.WriteLine($"[done] {scenario.CompetitionId}");
        }

        Console.WriteLine("[done] Dev scenarios prepared.");
        Console.WriteLine("[hint] Open http://localhost:5010 and use the dev-* competitions.");
        return 0;
    }

    public static void PrintScenarioList()
    {
        Console.WriteLine("""
            Available scenarios:
              all-states
              open
              open-with-submissions
              locked-no-results
              partial-results
              final-results
              empty

            Direct competition aliases:
              dev-open
              dev-open-with-submissions
              dev-locked-no-results
              dev-partial-results
              dev-final-results
              dev-empty
            """);
    }

    private static string? ValidateDevGuard(DevScenarioOptions opts)
    {
        if (!opts.Local)
        {
            return "Dev scenarios require the explicit --local flag.";
        }

        if (string.IsNullOrWhiteSpace(opts.SupabaseUrl) || string.IsNullOrWhiteSpace(opts.SupabaseSecretKey))
        {
            return "SUPABASE_URL and SUPABASE_SECRET_KEY must be set.";
        }

        if (!Uri.TryCreate(opts.SupabaseUrl, UriKind.Absolute, out var uri))
        {
            return $"SUPABASE_URL is not a valid absolute URL: {opts.SupabaseUrl}";
        }

        if (!uri.IsLoopback)
        {
            return "Dev scenarios can run only against a loopback Supabase URL.";
        }

        return null;
    }

    private static IReadOnlyList<DevCompetitionScenario> ResolveScenarios(string scenarioName)
    {
        if (string.Equals(scenarioName, AllStatesScenarioName, StringComparison.OrdinalIgnoreCase))
        {
            return Scenarios;
        }

        return Scenarios
            .Where(scenario =>
                string.Equals(scenario.CompetitionId, scenarioName, StringComparison.OrdinalIgnoreCase) ||
                scenario.Aliases.Any(alias => string.Equals(alias, scenarioName, StringComparison.OrdinalIgnoreCase)))
            .ToArray();
    }

    private static async Task<Competition?> LoadCompetitionAsync(string path, CancellationToken ct)
    {
        await using var stream = File.OpenRead(path);
        var competition = await JsonSerializer.DeserializeAsync<Competition>(
            stream,
            JsonOptions,
            ct);
        if (competition is null)
        {
            Console.Error.WriteLine($"[error] Could not parse base competition JSON: {path}");
        }

        return competition;
    }

    private static async Task ResetScenarioDataAsync(
        SupabaseRestClient supabase,
        IEnumerable<string> competitionIds,
        CancellationToken ct)
    {
        foreach (var competitionId in competitionIds.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var escaped = Uri.EscapeDataString(competitionId);
            await supabase.DeleteAsync("public", "score_snapshots", $"competition_id=eq.{escaped}", ct);
            await supabase.DeleteAsync("public", "official_results", $"competition_id=eq.{escaped}", ct);
            await supabase.DeleteAsync("public", "prediction_submissions", $"competition_id=eq.{escaped}", ct);
            await supabase.PatchAsync(
                "public",
                "competitions",
                $"id=eq.{escaped}",
                new JsonObject { ["published_version_id"] = null },
                ct);
            await supabase.DeleteAsync("public", "competition_versions", $"competition_id=eq.{escaped}", ct);
            await supabase.DeleteAsync("public", "competitions", $"id=eq.{escaped}", ct);
        }
    }

    private static async Task<IReadOnlyList<ScenarioUser>> EnsureScenarioUsersAsync(
        SupabaseAuthAdminClient auth,
        SupabaseRestClient supabase,
        CancellationToken ct)
    {
        var users = new List<ScenarioUser>();
        foreach (var scenarioUser in ScenarioUsers)
        {
            var userId = await auth.EnsureUserAsync(scenarioUser.Email, scenarioUser.DisplayName, ct);
            users.Add(new ScenarioUser(userId, scenarioUser.Email, scenarioUser.DisplayName));

            await supabase.UpsertAsync(
                "public",
                "profiles",
                "id",
                new JsonArray
                {
                    new JsonObject
                    {
                        ["id"] = userId,
                        ["display_name"] = scenarioUser.DisplayName
                    }
                },
                ct);
        }

        return users;
    }

    private static Competition CreateCompetition(
        Competition baseCompetition,
        DevCompetitionScenario scenario)
    {
        return baseCompetition with
        {
            Id = scenario.CompetitionId,
            Slug = scenario.CompetitionId,
            Name = scenario.Name,
            Description = $"Development scenario: {scenario.CompetitionId}.",
            Status = scenario.Status,
            StartDate = scenario.StartDate,
            EndDate = scenario.EndDate,
            PredictionOpenAt = scenario.PredictionOpenAt,
            PredictionLockAt = scenario.PredictionLockAt,
            ConfigVersion = DevConfigVersion
        };
    }

    private static async Task PublishCompetitionAsync(
        SupabaseRestClient supabase,
        Competition competition,
        DevCompetitionScenario scenario,
        CancellationToken ct)
    {
        var configNode = JsonSerializer.SerializeToNode(competition, JsonOptions)
            ?? throw new InvalidOperationException("Could not serialize competition config.");
        var summaryNode = JsonSerializer.SerializeToNode(
            new CompetitionSummary
            {
                Id = competition.Id,
                Slug = competition.Slug,
                Name = competition.Name,
                Description = competition.Description,
                Status = competition.Status,
                StartDate = competition.StartDate,
                PredictionLockAt = competition.PredictionLockAt,
                CardBackgroundImageUrl = competition.CardBackgroundImageUrl,
                CardBackgroundPosition = competition.CardBackgroundPosition,
                CardLogoImageUrl = competition.CardLogoImageUrl,
                CardLogoAlt = competition.CardLogoAlt,
                ConfigVersion = competition.ConfigVersion,
                City = "Dev Lab",
                CountryCode = "US",
                ModulesCount = competition.PredictionGroups.Count
            },
            JsonOptions);

        await supabase.UpsertAsync(
            "public",
            "competitions",
            "id",
            new JsonArray
            {
                new JsonObject
                {
                    ["id"] = competition.Id,
                    ["slug"] = competition.Slug,
                    ["name"] = competition.Name,
                    ["federation"] = competition.Federation,
                    ["status"] = ToDbStatus(scenario.Status),
                    ["start_date"] = scenario.StartDate.ToString("o"),
                    ["end_date"] = scenario.EndDate.ToString("o"),
                    ["prediction_open_at"] = scenario.PredictionOpenAt.ToString("o"),
                    ["prediction_lock_at"] = scenario.PredictionLockAt.ToString("o"),
                    ["summary"] = summaryNode
                }
            },
            ct);

        var returned = await supabase.InsertReturningAsync(
            "public",
            "competition_versions",
            new JsonArray
            {
                new JsonObject
                {
                    ["competition_id"] = competition.Id,
                    ["version"] = competition.ConfigVersion,
                    ["config"] = configNode,
                    ["published_at"] = DateTimeOffset.UtcNow.ToString("o")
                }
            },
            ct);

        var versionId = returned.OfType<JsonObject>().FirstOrDefault()?["id"]?.ToString();
        if (string.IsNullOrWhiteSpace(versionId))
        {
            throw new InvalidOperationException($"competition_versions insert returned no id for {competition.Id}.");
        }

        await supabase.PatchAsync(
            "public",
            "competitions",
            $"id=eq.{Uri.EscapeDataString(competition.Id)}",
            new JsonObject { ["published_version_id"] = versionId },
            ct);
    }

    private static async Task SeedSubmittedPredictionsAsync(
        SupabaseRestClient supabase,
        Competition competition,
        IReadOnlyList<ScenarioUser> users,
        CancellationToken ct)
    {
        var rows = new JsonArray();
        for (var index = 0; index < users.Count; index++)
        {
            var user = users[index];
            var predictionSet = CreatePredictionSet(competition, user.Id, index);
            rows.Add(new JsonObject
            {
                ["user_id"] = user.Id,
                ["competition_id"] = competition.Id,
                ["status"] = PredictionSet.SubmittedSubmissionStatus,
                ["answers_json"] = JsonSerializer.SerializeToNode(predictionSet, JsonOptions),
                ["app_version"] = DevAppVersion,
                ["schema_version"] = PredictionSet.StorageSchemaVersion,
                ["submitted_at"] = SubmittedAt.AddMinutes(index).ToString("o")
            });
        }

        await supabase.UpsertAsync(
            "public",
            "prediction_submissions",
            "user_id,competition_id",
            rows,
            ct);
    }

    private static PredictionSet CreatePredictionSet(Competition competition, string userId, int userIndex)
    {
        var answers = ScoreableQuestions(competition)
            .Select(item => new PredictionAnswer
            {
                GroupId = item.Group.Id,
                QuestionId = item.Question.Id,
                QuestionType = item.Question.Type,
                UpdatedAt = SubmittedAt,
                Value = new PredictionAnswerValue
                {
                    AthletePlacements = BuildPredictedPlacements(item.Question, userIndex)
                }
            })
            .ToArray();

        return new PredictionSet
        {
            CompetitionId = competition.Id,
            CompetitionConfigVersion = competition.ConfigVersion,
            LocalUserId = userId,
            AppVersion = DevAppVersion,
            SchemaVersion = PredictionSet.StorageSchemaVersion,
            SavedAt = SubmittedAt,
            SubmissionStatus = PredictionSet.SubmittedSubmissionStatus,
            SubmittedAt = null,
            Answers = answers
        };
    }

    private static IReadOnlyList<AthletePlacementPick> BuildPredictedPlacements(
        PredictionQuestion question,
        int userIndex)
    {
        var athleteIds = userIndex switch
        {
            1 when question.AthleteIds.Count >= 2 =>
                [question.AthleteIds[1], question.AthleteIds[0], .. question.AthleteIds.Skip(2)],
            2 when question.AthleteIds.Count >= 4 =>
                [.. question.AthleteIds.Skip(1), question.AthleteIds[0]],
            _ => question.AthleteIds
        };

        return athleteIds
            .Select((athleteId, index) => new AthletePlacementPick
            {
                Position = index + 1,
                AthleteId = athleteId,
                IsScored = index < GetRequiredCount(question),
                IsAutoSeeded = false,
                PredictedSquatKg = 200m - index,
                PredictedBenchKg = 120m - index,
                PredictedDeadliftKg = 240m - index,
                PredictedTotalKg = 560m - (index * 3)
            })
            .ToArray();
    }

    private static async Task<int> ImportResultsAsync(
        DevCompetitionScenario scenario,
        Competition competition,
        DevScenarioOptions opts,
        CancellationToken ct)
    {
        var resultsFile = CreateResultsFile(competition, scenario);
        var tempPath = Path.Combine(
            Path.GetTempPath(),
            $"totalcall-{scenario.CompetitionId}-{Guid.NewGuid():N}-results.json");
        await File.WriteAllTextAsync(
            tempPath,
            JsonSerializer.Serialize(resultsFile, JsonOptions),
            ct);

        try
        {
            return await new OfficialResultsImporter().RunAsync(
                new ResultsImportOptions
                {
                    CompetitionId = scenario.CompetitionId,
                    ResultsJsonPath = tempPath,
                    SupabaseUrl = opts.SupabaseUrl,
                    SupabaseSecretKey = opts.SupabaseSecretKey,
                    TriggeredBy = opts.TriggeredBy
                },
                ct);
        }
        finally
        {
            TryDelete(tempPath);
        }
    }

    private static OfficialResultsFile CreateResultsFile(
        Competition competition,
        DevCompetitionScenario scenario)
    {
        var scoreableQuestions = ScoreableQuestions(competition).ToArray();
        var takeCount = scenario.Results.FinalGroupCount ?? scoreableQuestions.Length;
        var groups = scoreableQuestions
            .Take(takeCount)
            .Select(item => new OfficialResultGroupFile
            {
                GroupId = item.Group.Id,
                QuestionId = item.Question.Id,
                CategoryId = item.Question.CategoryId,
                Status = OfficialResultGroupImportStatus.Final,
                Placements = CreateOfficialPlacements(item.Question)
            })
            .ToList();

        return new OfficialResultsFile
        {
            CompetitionId = competition.Id,
            Status = scenario.Results.Status ?? OfficialResultImportStatus.Partial,
            Source = "dev-scenarios-v1",
            Groups = groups
        };
    }

    private static List<OfficialResultPlacementFile> CreateOfficialPlacements(PredictionQuestion question)
    {
        return question.AthleteIds
            .Take(GetRequiredCount(question))
            .Select((athleteId, index) => new OfficialResultPlacementFile
            {
                Position = index + 1,
                AthleteId = athleteId,
                SquatKg = 200m - index,
                BenchKg = 120m - index,
                DeadliftKg = 240m - index,
                TotalKg = 560m - (index * 3)
            })
            .ToList();
    }

    private static IEnumerable<ScoreableQuestion> ScoreableQuestions(Competition competition)
    {
        foreach (var group in competition.PredictionGroups.OrderBy(group => group.Order))
        {
            if (!group.Required)
            {
                continue;
            }

            foreach (var question in group.Questions.OrderBy(question => question.Order))
            {
                if (!question.Required || question.AthleteIds.Count == 0)
                {
                    continue;
                }

                if (question.Type is PredictionQuestionType.AthleteRanking or PredictionQuestionType.CategoryPodium)
                {
                    yield return new ScoreableQuestion(group, question);
                }
            }
        }
    }

    private static int GetRequiredCount(PredictionQuestion question)
    {
        return question.Constraints.ExactSelections
               ?? question.Constraints.MaxSelections
               ?? 3;
    }

    private static string ToDbStatus(CompetitionStatus status) => status switch
    {
        CompetitionStatus.Locked => "locked",
        CompetitionStatus.Completed => "completed",
        CompetitionStatus.Archived => "archived",
        _ => "upcoming"
    };

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch
        {
            // Best-effort cleanup for local temp files.
        }
    }

    private sealed record DevCompetitionScenario(
        string CompetitionId,
        IReadOnlyList<string> Aliases,
        string Name,
        CompetitionStatus Status,
        DateTimeOffset PredictionOpenAt,
        DateTimeOffset StartDate,
        DateTimeOffset PredictionLockAt,
        DateTimeOffset EndDate,
        int SubmittedUsersCount,
        ResultsMode Results);

    private sealed record ResultsMode(string? Status, int? FinalGroupCount)
    {
        public static ResultsMode None { get; } = new(null, null);

        public bool HasResults => Status is not null;
    }

    private sealed record DevScenarioUser(string Email, string DisplayName);

    private sealed record ScenarioUser(string Id, string Email, string DisplayName);

    private sealed record ScoreableQuestion(PredictionGroup Group, PredictionQuestion Question);
}

internal sealed class SupabaseAuthAdminClient(string baseUrl, string serviceKey)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient http = new()
    {
        BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/"),
        Timeout = TimeSpan.FromMinutes(2)
    };

    public async Task<string> EnsureUserAsync(
        string email,
        string displayName,
        CancellationToken ct)
    {
        var existing = await FindUserIdByEmailAsync(email, ct);
        if (!string.IsNullOrWhiteSpace(existing))
        {
            return existing;
        }

        using var request = BuildRequest(HttpMethod.Post, "auth/v1/admin/users");
        request.Content = JsonContent.Create(
            new AdminCreateUserBody
            {
                Email = email,
                Password = CreateTemporaryPassword(),
                EmailConfirm = true,
                UserMetadata = new Dictionary<string, string>
                {
                    ["display_name"] = displayName
                }
            },
            options: JsonOptions);

        using var response = await http.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            // A concurrent or previous run may have created the user after the
            // first list call. Re-list before failing.
            existing = await FindUserIdByEmailAsync(email, ct);
            if (!string.IsNullOrWhiteSpace(existing))
            {
                return existing;
            }

            throw new InvalidOperationException(
                $"Auth admin create user failed for {email}: {(int)response.StatusCode} {response.ReasonPhrase} — {body}");
        }

        var created = JsonSerializer.Deserialize<AdminUserRow>(body, JsonOptions);
        if (string.IsNullOrWhiteSpace(created?.Id))
        {
            throw new InvalidOperationException($"Auth admin create user returned no id for {email}.");
        }

        return created.Id;
    }

    private static string CreateTemporaryPassword()
    {
        return $"tc-dev-{Guid.NewGuid():N}-A1!";
    }

    private async Task<string?> FindUserIdByEmailAsync(string email, CancellationToken ct)
    {
        using var request = BuildRequest(HttpMethod.Get, "auth/v1/admin/users?per_page=1000&page=1");
        using var response = await http.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Auth admin list users failed: {(int)response.StatusCode} {response.ReasonPhrase} — {body}");
        }

        var users = JsonSerializer.Deserialize<AdminUsersResponse>(body, JsonOptions);
        return users?.Users.FirstOrDefault(user =>
            string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase))?.Id;
    }

    private HttpRequestMessage BuildRequest(HttpMethod method, string url)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.TryAddWithoutValidation("apikey", serviceKey);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", serviceKey);
        return request;
    }

    private sealed record AdminUsersResponse
    {
        public IReadOnlyList<AdminUserRow> Users { get; init; } = [];
    }

    private sealed record AdminUserRow
    {
        public string Id { get; init; } = "";

        public string Email { get; init; } = "";
    }

    private sealed record AdminCreateUserBody
    {
        public required string Email { get; init; }

        public required string Password { get; init; }

        [JsonPropertyName("email_confirm")]
        public bool EmailConfirm { get; init; }

        [JsonPropertyName("user_metadata")]
        public Dictionary<string, string> UserMetadata { get; init; } = [];
    }
}
