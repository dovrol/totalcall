namespace TotalCall.Client.Domain.Predictions.Review;

public sealed record ReviewCompletionStats(
    int CompletedModules,
    int TotalModules,
    int CompletedSections,
    int TotalSections,
    int SavedPicks)
{
    public int RemainingSections => Math.Max(0, TotalSections - CompletedSections);

    public int RemainingModules => Math.Max(0, TotalModules - CompletedModules);
}
