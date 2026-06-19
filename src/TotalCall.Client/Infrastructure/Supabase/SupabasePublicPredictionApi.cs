using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using TotalCall.Client.Infrastructure.Json;
using TotalCall.Core.Domain.Predictions;
using TotalCall.Core.Domain.Predictions.Results;

namespace TotalCall.Client.Infrastructure.Supabase;

internal sealed class SupabasePublicPredictionApi(SupabasePredictionApiClient apiClient)
{
    public async Task<IReadOnlyList<PublicPredictionParticipant>> GetParticipantsAsync(
        string competitionId,
        CancellationToken cancellationToken = default)
    {
        using var request = apiClient.BuildPublicRequest(
            HttpMethod.Post,
            "rest/v1/rpc/get_competition_participants");
        request.Content = JsonContent.Create(
            new CompetitionRpcBody { CompetitionId = competitionId },
            options: SupabasePredictionJson.SupabaseJsonOptions);

        using var response = await apiClient.SendAsync(request, cancellationToken);
        await SupabasePredictionApiClient.EnsureSuccessAsync(response, cancellationToken);

        var rows = await response.Content.ReadFromJsonAsync<List<PublicPredictionParticipantRow>>(
            SupabasePredictionJson.SupabaseJsonOptions,
            cancellationToken);

        return rows?
            .Where(row => SupabasePredictionJson.IsSubmitted(row.Status, row.SubmittedAt))
            .Select((row, index) => new PublicPredictionParticipant(
                row.CompetitionId,
                SupabasePredictionJson.NormalizeParticipantDisplayName(row.DisplayName, index),
                row.SubmittedAt!.Value,
                PredictionSet.SubmittedSubmissionStatus))
            .ToArray() ?? [];
    }

    public async Task<IReadOnlyList<PublicCompetitionLeaderboardEntry>> GetLeaderboardAsync(
        string competitionId,
        CancellationToken cancellationToken = default)
    {
        using var request = apiClient.BuildPublicRequest(
            HttpMethod.Post,
            "rest/v1/rpc/get_competition_leaderboard");
        request.Content = JsonContent.Create(
            new CompetitionRpcBody { CompetitionId = competitionId },
            options: SupabasePredictionJson.SupabaseJsonOptions);

        using var response = await apiClient.SendAsync(request, cancellationToken);
        await SupabasePredictionApiClient.EnsureSuccessAsync(response, cancellationToken);

        var rows = await response.Content.ReadFromJsonAsync<List<PublicCompetitionLeaderboardRow>>(
            SupabasePredictionJson.SupabaseJsonOptions,
            cancellationToken);

        return rows?
            .Where(row => row.Position > 0)
            .Select(row => new PublicCompetitionLeaderboardEntry(
                row.Position,
                SupabasePredictionJson.NormalizeParticipantDisplayName(row.DisplayName, row.Position - 1),
                row.TotalPoints,
                row.ScoredGroupsCount,
                row.TotalGroupsCount,
                SupabasePredictionJson.NormalizeScoreStatus(row.Status),
                row.LastCalculatedAt,
                row.BoardRef))
            .ToArray() ?? [];
    }

    public async Task<PublicBoardResult?> GetPublicBoardAsync(
        string competitionId,
        string boardRef,
        CancellationToken cancellationToken = default)
    {
        using var request = apiClient.BuildPublicRequest(
            HttpMethod.Post,
            "rest/v1/rpc/get_public_board");
        request.Content = JsonContent.Create(
            new GetPublicBoardBody { CompetitionId = competitionId, BoardRef = boardRef },
            options: SupabasePredictionJson.SupabaseJsonOptions);

        using var response = await apiClient.SendAsync(request, cancellationToken);
        await SupabasePredictionApiClient.EnsureSuccessAsync(response, cancellationToken);

        var rows = await response.Content.ReadFromJsonAsync<List<PublicBoardRow>>(
            SupabasePredictionJson.SupabaseJsonOptions,
            cancellationToken);

        var row = rows?.FirstOrDefault();
        if (row?.PicksJson is not { ValueKind: JsonValueKind.Array } picksJson)
        {
            return null;
        }

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
            Status = SupabasePredictionJson.NormalizeScoreStatus(row.Status),
            LastCalculatedAt = row.LastCalculatedAt,
            Categories = SupabasePredictionJson.ParseCategories(row.BreakdownJson)
        };

        return new PublicBoardResult(
            SupabasePredictionJson.NormalizeParticipantDisplayName(row.DisplayName, 0),
            predictionSet,
            snapshot);
    }

    private sealed record CompetitionRpcBody
    {
        [JsonPropertyName("p_competition_id")]
        public required string CompetitionId { get; init; }
    }

    private sealed record GetPublicBoardBody
    {
        [JsonPropertyName("p_competition_id")]
        public required string CompetitionId { get; init; }

        [JsonPropertyName("p_board_ref")]
        public required string BoardRef { get; init; }
    }

    private sealed record PublicPredictionParticipantRow
    {
        public required string CompetitionId { get; init; }

        public string? DisplayName { get; init; }

        public DateTimeOffset? SubmittedAt { get; init; }

        public string? Status { get; init; }
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
}
