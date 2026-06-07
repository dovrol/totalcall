using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using TotalCall.Client.Application.Auth;
using TotalCall.Client.Domain.Predictions;
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

    private sealed record PublicPredictionParticipantRow
    {
        public required string CompetitionId { get; init; }

        public string? DisplayName { get; init; }

        public DateTimeOffset? SubmittedAt { get; init; }

        public string? Status { get; init; }
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
