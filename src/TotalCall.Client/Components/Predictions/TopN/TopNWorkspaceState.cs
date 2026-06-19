using TotalCall.Core.Domain.Competitions;

namespace TotalCall.Client.Components.Predictions.TopN;

public sealed record TopNWorkspaceState(
    PredictionDeadlinePhase DeadlinePhase,
    bool IsPublicBoard,
    bool CanEditSheet,
    bool ShowContextHeader,
    string BoardModeClass)
{
    public const string EditableBoardClass = "";
    public const string PublicBoardClass = "topn-workspace--public";
    public const string ReviewBoardClass = "topn-workspace--review";

    public static TopNWorkspaceState Resolve(
        Competition competition,
        bool canEditPredictions,
        string? publicBoardName,
        DateTimeOffset now)
    {
        var deadlinePhase = PredictionDeadline.Resolve(
            competition.Status,
            competition.PredictionLockAt,
            competition.EndDate,
            now);
        var isPublicBoard = !string.IsNullOrWhiteSpace(publicBoardName);
        var canEditSheet = !isPublicBoard &&
                           canEditPredictions &&
                           deadlinePhase is
                               PredictionDeadlinePhase.Open or
                               PredictionDeadlinePhase.Soon or
                               PredictionDeadlinePhase.Urgent;
        var boardModeClass = isPublicBoard
            ? PublicBoardClass
            : canEditSheet
                ? EditableBoardClass
                : ReviewBoardClass;

        return new TopNWorkspaceState(
            deadlinePhase,
            isPublicBoard,
            canEditSheet,
            isPublicBoard || !canEditSheet,
            boardModeClass);
    }

    public static TimeSpan GetDeadlineTickDelay(DateTimeOffset? predictionLockAt, DateTimeOffset now)
    {
        if (predictionLockAt is null)
        {
            return TimeSpan.FromMinutes(15);
        }

        var remaining = predictionLockAt.Value - now;

        if (remaining <= TimeSpan.FromHours(1))
        {
            return TimeSpan.FromSeconds(1);
        }

        if (remaining <= TimeSpan.FromHours(48))
        {
            return TimeSpan.FromMinutes(1);
        }

        return TimeSpan.FromMinutes(15);
    }
}
