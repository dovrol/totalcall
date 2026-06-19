using TotalCall.Client.Components.Predictions.TopN;
using TotalCall.Core.Domain.Predictions;

namespace TotalCall.Tests.Components.Predictions.TopN;

public sealed class TopNDragStateTests
{
    [Fact]
    public void Start_WithKnownPosition_SetsDraggingAndOverPosition()
    {
        var state = TopNDragState.Empty.Start(
            position: 2,
            [Pick("athlete-a", 1), Pick("athlete-b", 2)]);

        Assert.Equal(2, state.DraggingPosition);
        Assert.Equal(2, state.DragOverPosition);
    }

    [Fact]
    public void Start_WithUnknownPosition_KeepsCurrentState()
    {
        var current = new TopNDragState(1, 1);

        var state = current.Start(position: 3, [Pick("athlete-a", 1)]);

        Assert.Same(current, state);
    }

    [Fact]
    public void Enter_WithoutActiveDrag_KeepsState()
    {
        var state = TopNDragState.Empty.Enter(position: 2);

        Assert.Same(TopNDragState.Empty, state);
    }

    [Fact]
    public void Enter_WithActiveDrag_UpdatesOverPosition()
    {
        var state = new TopNDragState(1, 1).Enter(position: 3);

        Assert.Equal(1, state.DraggingPosition);
        Assert.Equal(3, state.DragOverPosition);
    }

    [Fact]
    public void Drop_WithoutActiveDrag_IsIgnored()
    {
        var (state, move) = TopNDragState.Empty.Drop(position: 2);

        Assert.Same(TopNDragState.Empty, state);
        Assert.Null(move);
    }

    [Fact]
    public void Drop_WithActiveDrag_ReturnsMoveAndClearsState()
    {
        var (state, move) = new TopNDragState(1, 3).Drop(position: 4);

        Assert.Same(TopNDragState.Empty, state);
        Assert.NotNull(move);
        Assert.Equal(1, move.From);
        Assert.Equal(4, move.To);
    }

    [Fact]
    public void End_ClearsState()
    {
        var state = new TopNDragState(1, 3).End();

        Assert.Same(TopNDragState.Empty, state);
    }

    private static AthletePlacementPick Pick(string athleteId, int position)
    {
        return new AthletePlacementPick
        {
            AthleteId = athleteId,
            Position = position
        };
    }
}
