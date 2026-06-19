using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using TotalCall.Core.Domain.Predictions;

namespace TotalCall.Client.Infrastructure.Supabase;

internal sealed class SupabasePredictionSubmitApi(SupabasePredictionApiClient apiClient)
{
    public async Task<PredictionSubmissionMetadata> SubmitAsync(
        PredictionSet predictionSet,
        CancellationToken cancellationToken = default)
    {
        var context = await apiClient.GetAuthenticatedContextAsync(cancellationToken);
        var body = new SubmitPredictionBody
        {
            CompetitionId = predictionSet.CompetitionId,
            AnswersJson = SupabasePredictionJson.SerializeSubmissionSnapshot(predictionSet with
            {
                SubmissionStatus = PredictionSet.SubmittedSubmissionStatus
            }),
            AppVersion = predictionSet.AppVersion,
            SchemaVersion = predictionSet.SchemaVersion
        };

        using var request = apiClient.BuildAuthenticatedRequest(
            HttpMethod.Post,
            "rest/v1/rpc/submit_prediction",
            context.AccessToken);

        request.Content = JsonContent.Create(body, options: SupabasePredictionJson.SupabaseJsonOptions);

        using var response = await apiClient.SendAsync(request, cancellationToken);
        await SupabasePredictionApiClient.EnsureSuccessAsync(response, cancellationToken);

        var rows = await response.Content.ReadFromJsonAsync<List<SubmitPredictionRow>>(
            SupabasePredictionJson.SupabaseJsonOptions,
            cancellationToken);

        var row = rows?.FirstOrDefault()
                  ?? throw new InvalidOperationException("Supabase submit did not return submission metadata.");

        return new PredictionSubmissionMetadata(
            SupabasePredictionJson.NormalizeSubmissionStatus(row.Status, row.SubmittedAt),
            row.SubmittedAt);
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
}
