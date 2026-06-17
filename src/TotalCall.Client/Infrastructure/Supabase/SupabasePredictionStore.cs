using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using TotalCall.Client.Application.Auth;
using TotalCall.Client.Domain.Predictions;
using TotalCall.Client.Domain.Predictions.Results;
using TotalCall.Client.Infrastructure.Json;

namespace TotalCall.Client.Infrastructure.Supabase;

public sealed class SupabasePredictionStore(
    HttpClient? httpClient,
    string publishableKey,
    AuthService authService)
{
    private static readonly JsonSerializerOptions SupabaseJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    // breakdown_json is authored by the sync tool with camelCase keys; match case-insensitively.
    private static readonly JsonSerializerOptions BreakdownJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<PredictionSet?> GetAsync(
        string competitionId,
        CancellationToken cancellationToken = default)
    {
        var context = await GetAuthenticatedContextAsync(cancellationToken);
        var url = "rest/v1/prediction_submissions"
                  + $"?competition_id=eq.{Uri.EscapeDataString(competitionId)}"
                  + "&select=answers_json,status,submitted_at"
                  + "&limit=1";

        using var request = BuildRequest(HttpMethod.Get, url, context.AccessToken);
        using var response = await httpClient!.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        var rows = await response.Content.ReadFromJsonAsync<List<SubmissionRow>>(
            SupabaseJsonOptions,
            cancellationToken);

        var row = rows?.FirstOrDefault();
        if (row?.AnswersJson is null)
        {
            return null;
        }

        var predictionSet = row.AnswersJson.Value.Deserialize<PredictionSet>(
            JsonDataOptions.SerializerOptions);

        return predictionSet is null
            ? null
            : predictionSet with
            {
                SubmissionStatus = NormalizeSubmissionStatus(row.Status, row.SubmittedAt),
                SubmittedAt = IsSubmitted(row.Status, row.SubmittedAt) ? row.SubmittedAt : null
            };
    }

    public async Task SaveDraftAsync(
        PredictionSet predictionSet,
        CancellationToken cancellationToken = default)
    {
        var context = await GetAuthenticatedContextAsync(cancellationToken);
        var body = new SubmissionUpsert
        {
            UserId = context.UserId,
            CompetitionId = predictionSet.CompetitionId,
            AnswersJson = SerializeSubmissionSnapshot(predictionSet),
            AppVersion = predictionSet.AppVersion,
            SchemaVersion = predictionSet.SchemaVersion
        };

        using var request = BuildRequest(
            HttpMethod.Post,
            "rest/v1/prediction_submissions?on_conflict=user_id,competition_id",
            context.AccessToken);

        request.Headers.TryAddWithoutValidation("Prefer", "resolution=merge-duplicates,return=minimal");
        request.Content = JsonContent.Create(body, options: SupabaseJsonOptions);

        using var response = await httpClient!.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    public async Task<PredictionSubmissionMetadata> SubmitAsync(
        PredictionSet predictionSet,
        CancellationToken cancellationToken = default)
    {
        var context = await GetAuthenticatedContextAsync(cancellationToken);
        var body = new SubmitPredictionBody
        {
            CompetitionId = predictionSet.CompetitionId,
            AnswersJson = SerializeSubmissionSnapshot(predictionSet with
            {
                SubmissionStatus = PredictionSet.SubmittedSubmissionStatus
            }),
            AppVersion = predictionSet.AppVersion,
            SchemaVersion = predictionSet.SchemaVersion
        };

        using var request = BuildRequest(
            HttpMethod.Post,
            "rest/v1/rpc/submit_prediction",
            context.AccessToken);

        request.Content = JsonContent.Create(body, options: SupabaseJsonOptions);

        using var response = await httpClient!.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        var rows = await response.Content.ReadFromJsonAsync<List<SubmitPredictionRow>>(
            SupabaseJsonOptions,
            cancellationToken);

        var row = rows?.FirstOrDefault()
                  ?? throw new InvalidOperationException("Supabase submit did not return submission metadata.");

        return new PredictionSubmissionMetadata(
            NormalizeSubmissionStatus(row.Status, row.SubmittedAt),
            row.SubmittedAt);
    }

    public async Task<MyScoreSnapshot?> GetMyScoreAsync(
        string competitionId,
        CancellationToken cancellationToken = default)
    {
        var context = await GetAuthenticatedContextAsync(cancellationToken);

        using var request = BuildRequest(
            HttpMethod.Post,
            "rest/v1/rpc/get_my_score",
            context.AccessToken);
        request.Content = JsonContent.Create(
            new GetParticipantsBody { CompetitionId = competitionId },
            options: SupabaseJsonOptions);

        using var response = await httpClient!.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        var rows = await response.Content.ReadFromJsonAsync<List<MyScoreRow>>(
            SupabaseJsonOptions,
            cancellationToken);

        var row = rows?.FirstOrDefault();
        if (row is null)
        {
            return null;
        }

        var categories = ParseCategories(row.BreakdownJson);

        return new MyScoreSnapshot
        {
            Rank = row.Rank,
            TotalPoints = row.TotalPoints,
            ScoredGroupsCount = row.ScoredGroupsCount,
            TotalGroupsCount = row.TotalGroupsCount,
            Status = NormalizeScoreStatus(row.Status),
            LastCalculatedAt = row.LastCalculatedAt,
            Categories = categories
        };
    }

    private static IReadOnlyList<CategoryScoreBreakdown> ParseCategories(JsonElement? breakdownJson)
    {
        if (breakdownJson is not { ValueKind: JsonValueKind.Object } breakdown)
        {
            return [];
        }

        var payload = breakdown.Deserialize<ScoreBreakdownPayload>(BreakdownJsonOptions);
        return payload?.QuestionScores ?? [];
    }

    public async Task<IReadOnlyList<PublicPredictionParticipant>> GetParticipantsAsync(
        string competitionId,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();

        using var request = BuildPublicRequest(
            HttpMethod.Post,
            "rest/v1/rpc/get_competition_participants");
        request.Content = JsonContent.Create(
            new GetParticipantsBody { CompetitionId = competitionId },
            options: SupabaseJsonOptions);

        using var response = await httpClient!.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        var rows = await response.Content.ReadFromJsonAsync<List<PublicPredictionParticipantRow>>(
            SupabaseJsonOptions,
            cancellationToken);

        return rows?
            .Where(row => IsSubmitted(row.Status, row.SubmittedAt))
            .Select((row, index) => new PublicPredictionParticipant(
                row.CompetitionId,
                NormalizeParticipantDisplayName(row.DisplayName, index),
                row.SubmittedAt!.Value,
                PredictionSet.SubmittedSubmissionStatus))
            .ToArray() ?? [];
    }

    public async Task<IReadOnlyList<PublicCompetitionLeaderboardEntry>> GetLeaderboardAsync(
        string competitionId,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();

        using var request = BuildPublicRequest(
            HttpMethod.Post,
            "rest/v1/rpc/get_competition_leaderboard");
        request.Content = JsonContent.Create(
            new GetParticipantsBody { CompetitionId = competitionId },
            options: SupabaseJsonOptions);

        using var response = await httpClient!.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        var rows = await response.Content.ReadFromJsonAsync<List<PublicCompetitionLeaderboardRow>>(
            SupabaseJsonOptions,
            cancellationToken);

        return rows?
            .Where(row => row.Position > 0)
            .Select(row => new PublicCompetitionLeaderboardEntry(
                row.Position,
                NormalizeParticipantDisplayName(row.DisplayName, row.Position - 1),
                row.TotalPoints,
                row.ScoredGroupsCount,
                row.TotalGroupsCount,
                NormalizeScoreStatus(row.Status),
                row.LastCalculatedAt,
                row.BoardRef))
            .ToArray() ?? [];
    }

    public async Task<PublicBoardResult?> GetPublicBoardAsync(
        string competitionId,
        string boardRef,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();

        using var request = BuildPublicRequest(
            HttpMethod.Post,
            "rest/v1/rpc/get_public_board");
        request.Content = JsonContent.Create(
            new GetPublicBoardBody { CompetitionId = competitionId, BoardRef = boardRef },
            options: SupabaseJsonOptions);

        using var response = await httpClient!.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        var rows = await response.Content.ReadFromJsonAsync<List<PublicBoardRow>>(
            SupabaseJsonOptions,
            cancellationToken);

        var row = rows?.FirstOrDefault();
        if (row?.PicksJson is not { ValueKind: JsonValueKind.Array } picksJson)
        {
            return null;
        }

        // The public RPC returns only the picks array (never the full PredictionSet),
        // so rebuild a minimal submitted PredictionSet that the board can render.
        var answers = picksJson.Deserialize<List<PredictionAnswer>>(JsonDataOptions.SerializerOptions)
                      ?? [];

        var predictionSet = new PredictionSet
        {
            CompetitionId = competitionId,
            CompetitionConfigVersion = string.Empty,
            SubmissionStatus = PredictionSet.SubmittedSubmissionStatus,
            Answers = answers
        };

        var snapshot = new MyScoreSnapshot
        {
            Rank = row.Rank,
            TotalPoints = row.TotalPoints,
            ScoredGroupsCount = row.ScoredGroupsCount,
            TotalGroupsCount = row.TotalGroupsCount,
            Status = NormalizeScoreStatus(row.Status),
            LastCalculatedAt = row.LastCalculatedAt,
            Categories = ParseCategories(row.BreakdownJson)
        };

        return new PublicBoardResult(
            NormalizeParticipantDisplayName(row.DisplayName, 0),
            predictionSet,
            snapshot);
    }

    private async Task<AuthenticatedContext> GetAuthenticatedContextAsync(CancellationToken cancellationToken)
    {
        EnsureConfigured();

        var session = await authService.GetSessionAsync(cancellationToken);
        if (session is null || string.IsNullOrWhiteSpace(session.User.Id))
        {
            throw new InvalidOperationException("Cloud prediction storage requires an authenticated user.");
        }

        return new AuthenticatedContext(session.User.Id, session.AccessToken);
    }

    private HttpRequestMessage BuildRequest(HttpMethod method, string url, string accessToken)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.TryAddWithoutValidation("apikey", publishableKey);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return request;
    }

    private HttpRequestMessage BuildPublicRequest(HttpMethod method, string url)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.TryAddWithoutValidation("apikey", publishableKey);
        return request;
    }

    private void EnsureConfigured()
    {
        if (httpClient is null || string.IsNullOrWhiteSpace(publishableKey))
        {
            throw new InvalidOperationException(
                "Supabase is not configured. Set Supabase:Url and Supabase:PublishableKey in wwwroot/appsettings.json.");
        }
    }

    private static async Task EnsureSuccessAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new HttpRequestException(
            string.IsNullOrWhiteSpace(body)
                ? $"Supabase prediction request failed ({(int)response.StatusCode})."
                : $"Supabase prediction request failed ({(int)response.StatusCode}): {body}",
            null,
            response.StatusCode);
    }

    private sealed record AuthenticatedContext(string UserId, string AccessToken);

    private sealed record SubmissionRow
    {
        public JsonElement? AnswersJson { get; init; }

        public string? Status { get; init; }

        public DateTimeOffset? SubmittedAt { get; init; }
    }

    private sealed record SubmissionUpsert
    {
        public required string UserId { get; init; }

        public required string CompetitionId { get; init; }

        public required JsonElement AnswersJson { get; init; }

        public required string AppVersion { get; init; }

        public required int SchemaVersion { get; init; }
    }

    private sealed record SubmitPredictionBody
    {
        [JsonPropertyName("p_competition_id")]
        public required string CompetitionId { get; init; }

        [JsonPropertyName("p_answers_json")]
        public required JsonElement AnswersJson { get; init; }

        [JsonPropertyName("p_app_version")]
        public required string AppVersion { get; init; }

        [JsonPropertyName("p_schema_version")]
        public required int SchemaVersion { get; init; }
    }

    private sealed record SubmitPredictionRow
    {
        public string? Status { get; init; }

        public DateTimeOffset? SubmittedAt { get; init; }
    }

    private sealed record GetParticipantsBody
    {
        [JsonPropertyName("p_competition_id")]
        public required string CompetitionId { get; init; }
    }

    private sealed record MyScoreRow
    {
        public int? Rank { get; init; }

        public decimal TotalPoints { get; init; }

        public int ScoredGroupsCount { get; init; }

        public int TotalGroupsCount { get; init; }

        public string? Status { get; init; }

        public JsonElement? BreakdownJson { get; init; }

        public DateTimeOffset? LastCalculatedAt { get; init; }
    }

    private sealed record ScoreBreakdownPayload
    {
        public List<CategoryScoreBreakdown>? QuestionScores { get; init; }
    }

    private sealed record PublicPredictionParticipantRow
    {
        public required string CompetitionId { get; init; }

        public string? DisplayName { get; init; }

        public DateTimeOffset? SubmittedAt { get; init; }

        public string? Status { get; init; }
    }

    private sealed record GetPublicBoardBody
    {
        [JsonPropertyName("p_competition_id")]
        public required string CompetitionId { get; init; }

        [JsonPropertyName("p_board_ref")]
        public required string BoardRef { get; init; }
    }

    private sealed record PublicBoardRow
    {
        public string? DisplayName { get; init; }

        public int? Rank { get; init; }

        public decimal TotalPoints { get; init; }

        public int ScoredGroupsCount { get; init; }

        public int TotalGroupsCount { get; init; }

        public string? Status { get; init; }

        public JsonElement? PicksJson { get; init; }

        public JsonElement? BreakdownJson { get; init; }

        public DateTimeOffset? LastCalculatedAt { get; init; }
    }

    private sealed record PublicCompetitionLeaderboardRow
    {
        public int Position { get; init; }

        public string? BoardRef { get; init; }

        public string? DisplayName { get; init; }

        public decimal TotalPoints { get; init; }

        public int ScoredGroupsCount { get; init; }

        public int TotalGroupsCount { get; init; }

        public string? Status { get; init; }

        public DateTimeOffset LastCalculatedAt { get; init; }
    }

    private static string NormalizeSubmissionStatus(string? status, DateTimeOffset? submittedAt)
    {
        return IsSubmitted(status, submittedAt)
            ? PredictionSet.SubmittedSubmissionStatus
            : PredictionSet.DraftSubmissionStatus;
    }

    private static bool IsSubmitted(string? status, DateTimeOffset? submittedAt)
    {
        return submittedAt is not null ||
               string.Equals(
                   status,
                   PredictionSet.SubmittedSubmissionStatus,
                   StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeParticipantDisplayName(string? displayName, int index)
    {
        if (!string.IsNullOrWhiteSpace(displayName))
        {
            return displayName.Trim();
        }

        return SupabaseProfileStore.MissingDisplayNameFallback;
    }

    private static string NormalizeScoreStatus(string? status)
    {
        return string.Equals(status, PublicCompetitionLeaderboardEntry.FinalStatus, StringComparison.OrdinalIgnoreCase)
            ? PublicCompetitionLeaderboardEntry.FinalStatus
            : PublicCompetitionLeaderboardEntry.PartialStatus;
    }

    private static JsonElement SerializeSubmissionSnapshot(PredictionSet predictionSet)
    {
        return JsonSerializer.SerializeToElement(
            predictionSet with { SubmittedAt = null },
            JsonDataOptions.SerializerOptions);
    }
}

public sealed record PredictionSubmissionMetadata(string Status, DateTimeOffset? SubmittedAt);

public sealed record PublicPredictionParticipant(
    string CompetitionId,
    string DisplayName,
    DateTimeOffset SubmittedAt,
    string Status);

public sealed record PublicCompetitionLeaderboardEntry(
    int Position,
    string DisplayName,
    decimal TotalPoints,
    int ScoredGroupsCount,
    int TotalGroupsCount,
    string Status,
    DateTimeOffset LastCalculatedAt,
    string? BoardRef = null)
{
    public const string PartialStatus = "partial";
    public const string FinalStatus = "final";
}

public sealed record PublicBoardResult(
    string DisplayName,
    PredictionSet PredictionSet,
    MyScoreSnapshot Snapshot);
