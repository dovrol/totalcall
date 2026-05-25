namespace TotalCall.Client.Application.Windows;

public sealed record AthleteHistorySlotContext(
    int SlotPosition,
    string EditorAthleteId,
    bool CanUseLast,
    bool CanUseBest,
    bool CanUseNominated);
