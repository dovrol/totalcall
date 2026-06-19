using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using TotalCall.Core.Domain.Predictions.Results;

namespace TotalCall.Client.Infrastructure.Supabase;

internal sealed class SupabaseAuthenticatedScoreApi(SupabasePredictionApiClient apiClient)
{
    public async Task<MyScoreSnapshot?> GetMyScoreAsync(
        string competitionId,
        CancellationToken cancellationToken = default)
    {
        var context = await apiClient.GetAuthenticatedContextAsync(cancellationToken);

        using var request = apiClient.BuildAuthenticatedRequest(
            HttpMethod.Post,
            "rest/v1/rpc/get_my_score",
            context.AccessToken);
        request.Content = JsonContent.Create(
            new GetMyScoreBody { CompetitionId = competitionId },
            options: SupabasePredictionJson.SupabaseJsonOptions);

        using var response = await apiClient.SendAsync(request, cancellationToken);
        await SupabasePredictionApiClient.EnsureSuccessAsync(response, cancellationToken);

        var rows = await response.Content.ReadFromJsonAsync<List<MyScoreRow>>(
            SupabasePredictionJson.SupabaseJsonOptions,
            cancellationToken);

        var row = rows?.FirstOrDefault();
        if (row is null)
        {
            return null;
        }

        return new MyScoreSnapshot
        {
            Rank = row.Rank,
            TotalPoints = row.TotalPoints,
            ScoredGroupsCount = row.ScoredGroupsCount,
            TotalGroupsCount = row.TotalGroupsCount,
            Status = SupabasePredictionJson.NormalizeScoreStatus(row.Status),
            LastCalculatedAt = row.LastCalculatedAt,
            Categories = SupabasePredictionJson.ParseCategories(row.BreakdownJson)
        };
    }

    private sealed record GetMyScoreBody
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
}
