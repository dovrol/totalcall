using TotalCall.Core.Domain.Competitions;

namespace TotalCall.Client.Components.Predictions;

public enum PredictionDeadlinePhase
{
    Open,
    Soon,
    Urgent,
    Locked,
    Ended
}

public static class PredictionDeadline
{
    public static readonly TimeSpan SoonThreshold = TimeSpan.FromHours(48);
    public static readonly TimeSpan UrgentThreshold = TimeSpan.FromHours(1);

    public static PredictionDeadlinePhase Resolve(
        CompetitionStatus status,
        DateTimeOffset? predictionLockAt,
        DateTimeOffset? endDate,
        DateTimeOffset now)
    {
        if (status is CompetitionStatus.Completed or CompetitionStatus.Archived ||
            (endDate is not null && now > endDate))
        {
            return PredictionDeadlinePhase.Ended;
        }

        if (status == CompetitionStatus.Locked)
        {
            return PredictionDeadlinePhase.Locked;
        }

        if (predictionLockAt is null)
        {
            return PredictionDeadlinePhase.Open;
        }

        var remaining = predictionLockAt.Value - now;
        if (remaining <= TimeSpan.Zero)
        {
            return PredictionDeadlinePhase.Locked;
        }

        if (remaining <= UrgentThreshold)
        {
            return PredictionDeadlinePhase.Urgent;
        }

        return remaining <= SoonThreshold
            ? PredictionDeadlinePhase.Soon
            : PredictionDeadlinePhase.Open;
    }

    public static bool CanEdit(
        bool configuredCanEdit,
        CompetitionStatus status,
        DateTimeOffset? predictionLockAt,
        DateTimeOffset? endDate,
        DateTimeOffset now)
    {
        if (!configuredCanEdit)
        {
            return false;
        }

        return Resolve(status, predictionLockAt, endDate, now) is
            PredictionDeadlinePhase.Open or
            PredictionDeadlinePhase.Soon or
            PredictionDeadlinePhase.Urgent;
    }

    public static TimeSpan? Remaining(DateTimeOffset? predictionLockAt, DateTimeOffset now)
    {
        if (predictionLockAt is null)
        {
            return null;
        }

        return predictionLockAt.Value - now;
    }
}
