using System.Globalization;
using TotalCall.Core.Domain.Predictions;

namespace TotalCall.Client.Components.Predictions.TopN;

public enum TopNSheetEditPlanStatus
{
    Applied,
    Ignored,
    InvalidValue
}

public sealed record TopNSheetEditPlan(
    TopNSheetEditPlanStatus Status,
    IReadOnlyList<AthletePlacementPick> Placements)
{
    public static TopNSheetEditPlan Ignored { get; } = new(TopNSheetEditPlanStatus.Ignored, []);

    public static TopNSheetEditPlan InvalidValue { get; } = new(TopNSheetEditPlanStatus.InvalidValue, []);
}

public static class TopNSheetEditPlanner
{
    public static TopNSheetEditPlan ApplyEdit(
        IReadOnlyList<AthletePlacementPick> placements,
        TopNSheetEdit edit,
        CultureInfo culture)
    {
        if (!TopNPlacementEditor.TryReadOptionalPositiveDecimal(edit.Value, culture, out var parsed))
        {
            return TopNSheetEditPlan.InvalidValue;
        }

        if (placements.All(placement => placement.Position != edit.Position))
        {
            return TopNSheetEditPlan.Ignored;
        }

        return new TopNSheetEditPlan(
            TopNSheetEditPlanStatus.Applied,
            placements
                .Select(placement => placement.Position == edit.Position
                    ? TopNPlacementEditor.ApplyEdit(placement, edit.Field, parsed)
                    : placement)
                .ToArray());
    }

    public static TopNSheetEditPlan ApplyNudge(
        IReadOnlyList<AthletePlacementPick> placements,
        TopNSheetEdit edit)
    {
        if (!TopNPlacementEditor.TryReadNudgeDelta(edit.Value, out var delta) ||
            placements.All(placement => placement.Position != edit.Position))
        {
            return TopNSheetEditPlan.Ignored;
        }

        return new TopNSheetEditPlan(
            TopNSheetEditPlanStatus.Applied,
            placements
                .Select(placement => placement.Position == edit.Position
                    ? TopNPlacementEditor.ApplyNudge(placement, edit.Field, delta)
                    : placement)
                .ToArray());
    }
}
