using TotalCall.Core.Domain.Predictions;

namespace TotalCall.Client.Components.Predictions.TopN;

public sealed record TopNDropMove(int From, int To);

public sealed record TopNDragState(int? DraggingPosition, int? DragOverPosition)
{
    public static TopNDragState Empty { get; } = new(null, null);

    public TopNDragState Start(int position, IReadOnlyList<AthletePlacementPick> placements)
    {
        return placements.Any(placement => placement.Position == position)
            ? new TopNDragState(position, position)
            : this;
    }

    public TopNDragState Enter(int position)
    {
        return DraggingPosition is null
            ? this
            : this with { DragOverPosition = position };
    }

    public (TopNDragState State, TopNDropMove? Move) Drop(int position)
    {
        return DraggingPosition is { } from
            ? (Empty, new TopNDropMove(from, position))
            : (this, null);
    }

    public TopNDragState End()
    {
        return Empty;
    }
}
