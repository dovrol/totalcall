using System.Text.Json;
using System.Text.Json.Serialization;
using TotalCall.Client.Infrastructure.Json;
using TotalCall.Core.Domain.Predictions;
using TotalCall.Core.Domain.Predictions.Results;

namespace TotalCall.Client.Infrastructure.Supabase;

internal static class SupabasePredictionJson
{
    public static readonly JsonSerializerOptions SupabaseJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static readonly JsonSerializerOptions BreakdownJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static JsonElement SerializeSubmissionSnapshot(PredictionSet predictionSet)
    {
        return JsonSerializer.SerializeToElement(
            predictionSet with { SubmittedAt = null },
            JsonDataOptions.SerializerOptions);
    }

    public static IReadOnlyList<CategoryScoreBreakdown> ParseCategories(JsonElement? breakdownJson)
    {
        if (breakdownJson is not { ValueKind: JsonValueKind.Object } breakdown)
        {
            return [];
        }

        var payload = breakdown.Deserialize<ScoreBreakdownPayload>(BreakdownJsonOptions);
        return payload?.QuestionScores ?? [];
    }

    public static string NormalizeSubmissionStatus(string? status, DateTimeOffset? submittedAt)
    {
        return IsSubmitted(status, submittedAt)
            ? PredictionSet.SubmittedSubmissionStatus
            : PredictionSet.DraftSubmissionStatus;
    }

    public static bool IsSubmitted(string? status, DateTimeOffset? submittedAt)
    {
        return submittedAt is not null ||
               string.Equals(
                   status,
                   PredictionSet.SubmittedSubmissionStatus,
                   StringComparison.OrdinalIgnoreCase);
    }

    public static string NormalizeParticipantDisplayName(string? displayName, int _)
    {
        if (!string.IsNullOrWhiteSpace(displayName))
        {
            return displayName.Trim();
        }

        return SupabaseProfileStore.MissingDisplayNameFallback;
    }

    public static string NormalizeScoreStatus(string? status)
    {
        return string.Equals(status, PublicCompetitionLeaderboardEntry.FinalStatus, StringComparison.OrdinalIgnoreCase)
            ? PublicCompetitionLeaderboardEntry.FinalStatus
            : PublicCompetitionLeaderboardEntry.PartialStatus;
    }

    private sealed record ScoreBreakdownPayload
    {
        public List<CategoryScoreBreakdown>? QuestionScores { get; init; }
    }
}
