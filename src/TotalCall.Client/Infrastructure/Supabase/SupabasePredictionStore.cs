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
                  + "&select=answers_json"
                  + "&limit=1";

        using var request = BuildRequest(HttpMethod.Get, url, context.AccessToken);
        using var response = await httpClient!.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        var rows = await response.Content.ReadFromJsonAsync<List<SubmissionRow>>(
            SupabaseJsonOptions,
            cancellationToken);

        var snapshot = rows?.FirstOrDefault()?.AnswersJson;
        return snapshot is null
            ? null
            : snapshot.Value.Deserialize<PredictionSet>(JsonDataOptions.SerializerOptions);
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
            Status = "draft",
            AnswersJson = JsonSerializer.SerializeToElement(
                predictionSet,
                JsonDataOptions.SerializerOptions),
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

    private async Task<AuthenticatedContext> GetAuthenticatedContextAsync(CancellationToken cancellationToken)
    {
        if (httpClient is null || string.IsNullOrWhiteSpace(publishableKey))
        {
            throw new InvalidOperationException(
                "Supabase is not configured. Set Supabase:Url and Supabase:PublishableKey in wwwroot/appsettings.json.");
        }

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
        public JsonElement AnswersJson { get; init; }
    }

    private sealed record SubmissionUpsert
    {
        public required string UserId { get; init; }

        public required string CompetitionId { get; init; }

        public required string Status { get; init; }

        public required JsonElement AnswersJson { get; init; }

        public required string AppVersion { get; init; }

        public required int SchemaVersion { get; init; }
    }
}
