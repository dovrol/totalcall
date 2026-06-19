using TotalCall.Client.Components.Predictions;
using TotalCall.Client.Components.Predictions.TopN;
using TotalCall.Core.Domain.Competitions;

namespace TotalCall.Tests.Components.Predictions.TopN;

public sealed class TopNWorkspaceStateTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-06-19T12:00:00Z");

    [Fact]
    public void Resolve_WhenOpenAndEditable_ReturnsEditableBoardState()
    {
        var competition = CreateCompetition(
            CompetitionStatus.Upcoming,
            predictionLockAt: Now.AddDays(5));

        var state = TopNWorkspaceState.Resolve(competition, canEditPredictions: true, publicBoardName: " ", Now);

        Assert.Equal(PredictionDeadlinePhase.Open, state.DeadlinePhase);
        Assert.False(state.IsPublicBoard);
        Assert.True(state.CanEditSheet);
        Assert.False(state.ShowContextHeader);
        Assert.Equal(TopNWorkspaceState.EditableBoardClass, state.BoardModeClass);
    }

    [Fact]
    public void Resolve_WhenPublicBoard_ForcesReadOnlyPublicState()
    {
        var competition = CreateCompetition(
            CompetitionStatus.Upcoming,
            predictionLockAt: Now.AddDays(5));

        var state = TopNWorkspaceState.Resolve(competition, canEditPredictions: true, publicBoardName: "Kuba", Now);

        Assert.Equal(PredictionDeadlinePhase.Open, state.DeadlinePhase);
        Assert.True(state.IsPublicBoard);
        Assert.False(state.CanEditSheet);
        Assert.True(state.ShowContextHeader);
        Assert.Equal(TopNWorkspaceState.PublicBoardClass, state.BoardModeClass);
    }

    [Fact]
    public void Resolve_WhenLockPassed_ReturnsReviewState()
    {
        var competition = CreateCompetition(
            CompetitionStatus.Upcoming,
            predictionLockAt: Now.AddSeconds(-1));

        var state = TopNWorkspaceState.Resolve(competition, canEditPredictions: true, publicBoardName: null, Now);

        Assert.Equal(PredictionDeadlinePhase.Locked, state.DeadlinePhase);
        Assert.False(state.IsPublicBoard);
        Assert.False(state.CanEditSheet);
        Assert.True(state.ShowContextHeader);
        Assert.Equal(TopNWorkspaceState.ReviewBoardClass, state.BoardModeClass);
    }

    [Fact]
    public void Resolve_WhenConfiguredCannotEdit_ReturnsReviewStateEvenBeforeLock()
    {
        var competition = CreateCompetition(
            CompetitionStatus.Upcoming,
            predictionLockAt: Now.AddDays(5));

        var state = TopNWorkspaceState.Resolve(competition, canEditPredictions: false, publicBoardName: null, Now);

        Assert.Equal(PredictionDeadlinePhase.Open, state.DeadlinePhase);
        Assert.False(state.CanEditSheet);
        Assert.True(state.ShowContextHeader);
        Assert.Equal(TopNWorkspaceState.ReviewBoardClass, state.BoardModeClass);
    }

    [Fact]
    public void Resolve_WhenCompetitionCompleted_ReturnsEndedReviewState()
    {
        var competition = CreateCompetition(
            CompetitionStatus.Completed,
            predictionLockAt: Now.AddDays(5));

        var state = TopNWorkspaceState.Resolve(competition, canEditPredictions: true, publicBoardName: null, Now);

        Assert.Equal(PredictionDeadlinePhase.Ended, state.DeadlinePhase);
        Assert.False(state.CanEditSheet);
        Assert.True(state.ShowContextHeader);
        Assert.Equal(TopNWorkspaceState.ReviewBoardClass, state.BoardModeClass);
    }

    [Fact]
    public void GetDeadlineTickDelay_UsesShorterCadenceNearLock()
    {
        Assert.Equal(
            TimeSpan.FromSeconds(1),
            TopNWorkspaceState.GetDeadlineTickDelay(Now.AddMinutes(30), Now));
        Assert.Equal(
            TimeSpan.FromMinutes(1),
            TopNWorkspaceState.GetDeadlineTickDelay(Now.AddHours(12), Now));
        Assert.Equal(
            TimeSpan.FromMinutes(15),
            TopNWorkspaceState.GetDeadlineTickDelay(Now.AddDays(5), Now));
        Assert.Equal(
            TimeSpan.FromMinutes(15),
            TopNWorkspaceState.GetDeadlineTickDelay(null, Now));
    }

    private static Competition CreateCompetition(
        CompetitionStatus status,
        DateTimeOffset? predictionLockAt)
    {
        return new Competition
        {
            Id = "worlds-2026",
            Slug = "worlds-2026",
            Name = "Worlds 2026",
            ConfigVersion = "1",
            Status = status,
            PredictionLockAt = predictionLockAt
        };
    }
}
