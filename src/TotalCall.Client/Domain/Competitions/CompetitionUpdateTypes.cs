namespace TotalCall.Client.Domain.Competitions;

public static class CompetitionUpdateTypes
{
    public const string General = "general";
    public const string RosterUpdate = "roster_update";
    public const string DeadlineChange = "deadline_change";
    public const string ResultsUpdate = "results_update";
    public const string ScoringUpdate = "scoring_update";

    public static string Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? General
            : value.Trim().ToLowerInvariant();
    }
}
