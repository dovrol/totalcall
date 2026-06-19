using System.Net.Http.Json;
using System.Text.Json;
using TotalCall.Client.Infrastructure.Json;
using TotalCall.Core.Domain.Predictions;

namespace TotalCall.Client.Infrastructure.Supabase;

internal sealed class SupabasePredictionDraftApi(SupabasePredictionApiClient apiClient)
{
    public async Task<PredictionSet?> GetAsync(
        string competitionId,
        CancellationToken cancellationToken = default)
    {
        var context = await apiClient.GetAuthenticatedContextAsync(cancellationToken);
        var url = "rest/v1/prediction_submissions"
                  + $"?competition_id=eq.{Uri.EscapeDataString(competitionId)}"
                  + "&select=answers_json,status,submitted_at"
                  + "&limit=1";

        using var request = apiClient.BuildAuthenticatedRequest(
            HttpMethod.Get,
            url,
            context.AccessToken);
        using var response = await apiClient.SendAsync(request, cancellationToken);
        await SupabasePredictionApiClient.EnsureSuccessAsync(response, cancellationToken);

        var rows = await response.Content.ReadFromJsonAsync<List<SubmissionRow>>(
            SupabasePredictionJson.SupabaseJsonOptions,
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
                SubmissionStatus = SupabasePredictionJson.NormalizeSubmissionStatus(row.Status, row.SubmittedAt),
                SubmittedAt = SupabasePredictionJson.IsSubmitted(row.Status, row.SubmittedAt)
                    ? row.SubmittedAt
                    : null
            };
    }

    public async Task SaveDraftAsync(
        PredictionSet predictionSet,
        CancellationToken cancellationToken = default)
    {
        var context = await apiClient.GetAuthenticatedContextAsync(cancellationToken);
        var body = new SubmissionUpsert
        {
            UserId = context.UserId,
            CompetitionId = predictionSet.CompetitionId,
            AnswersJson = SupabasePredictionJson.SerializeSubmissionSnapshot(predictionSet),
            AppVersion = predictionSet.AppVersion,
            SchemaVersion = predictionSet.SchemaVersion
        };

        using var request = apiClient.BuildAuthenticatedRequest(
            HttpMethod.Post,
            "rest/v1/prediction_submissions?on_conflict=user_id,competition_id",
            context.AccessToken);

        request.Headers.TryAddWithoutValidation("Prefer", "resolution=merge-duplicates,return=minimal");
        request.Content = JsonContent.Create(body, options: SupabasePredictionJson.SupabaseJsonOptions);

        using var response = await apiClient.SendAsync(request, cancellationToken);
        await SupabasePredictionApiClient.EnsureSuccessAsync(response, cancellationToken);
    }

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
}
