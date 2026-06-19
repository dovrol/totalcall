using TotalCall.Client.Application.Auth;
using TotalCall.Core.Domain.Predictions;
using TotalCall.Core.Domain.Predictions.Results;

namespace TotalCall.Client.Infrastructure.Supabase;

public sealed class SupabasePredictionStore
{
    private readonly SupabasePredictionDraftApi draftApi;
    private readonly SupabasePredictionSubmitApi submitApi;
    private readonly SupabaseAuthenticatedScoreApi scoreApi;
    private readonly SupabasePublicPredictionApi publicApi;

    public SupabasePredictionStore(
        HttpClient? httpClient,
        string publishableKey,
        AuthService authService)
    {
        var apiClient = new SupabasePredictionApiClient(httpClient, publishableKey, authService);
        draftApi = new SupabasePredictionDraftApi(apiClient);
        submitApi = new SupabasePredictionSubmitApi(apiClient);
        scoreApi = new SupabaseAuthenticatedScoreApi(apiClient);
        publicApi = new SupabasePublicPredictionApi(apiClient);
    }

    public Task<PredictionSet?> GetAsync(
        string competitionId,
        CancellationToken cancellationToken = default)
    {
        return draftApi.GetAsync(competitionId, cancellationToken);
    }

    public Task SaveDraftAsync(
        PredictionSet predictionSet,
        CancellationToken cancellationToken = default)
    {
        return draftApi.SaveDraftAsync(predictionSet, cancellationToken);
    }

    public Task<PredictionSubmissionMetadata> SubmitAsync(
        PredictionSet predictionSet,
        CancellationToken cancellationToken = default)
    {
        return submitApi.SubmitAsync(predictionSet, cancellationToken);
    }

    public Task<MyScoreSnapshot?> GetMyScoreAsync(
        string competitionId,
        CancellationToken cancellationToken = default)
    {
        return scoreApi.GetMyScoreAsync(competitionId, cancellationToken);
    }

    public Task<IReadOnlyList<PublicPredictionParticipant>> GetParticipantsAsync(
        string competitionId,
        CancellationToken cancellationToken = default)
    {
        return publicApi.GetParticipantsAsync(competitionId, cancellationToken);
    }

    public Task<IReadOnlyList<PublicCompetitionLeaderboardEntry>> GetLeaderboardAsync(
        string competitionId,
        CancellationToken cancellationToken = default)
    {
        return publicApi.GetLeaderboardAsync(competitionId, cancellationToken);
    }

    public Task<PublicBoardResult?> GetPublicBoardAsync(
        string competitionId,
        string boardRef,
        CancellationToken cancellationToken = default)
    {
        return publicApi.GetPublicBoardAsync(competitionId, boardRef, cancellationToken);
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
