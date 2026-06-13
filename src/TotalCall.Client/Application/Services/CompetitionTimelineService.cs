using TotalCall.Client.Domain.Athletes;
using TotalCall.Client.Domain.Competitions;

namespace TotalCall.Client.Application.Services;

public sealed record CompetitionTimelineItem(
    string Id,
    string Type,
    DateTimeOffset? OccurredAt,
    string? Title,
    string? Body,
    string? Source,
    IReadOnlyList<Athlete> Athletes,
    bool IsGenerated);

public static class CompetitionTimelineService
{
    public static IReadOnlyList<CompetitionTimelineItem> GetTimeline(Competition competition)
    {
        var athletesById = competition.Athletes
            .ToDictionary(athlete => athlete.Id, StringComparer.OrdinalIgnoreCase);

        var items = competition.Updates
            .Select((update, index) => ToItem(update, index, athletesById))
            .ToList();

        var manuallyCoveredRosterAthletes = competition.Updates
            .Where(update => IsRosterUpdate(update.Type))
            .SelectMany(update => update.AthleteIds)
            .Where(athleteId => !string.IsNullOrWhiteSpace(athleteId))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var athlete in RosterUpdateService.GetWithdrawnAthletes(competition))
        {
            if (manuallyCoveredRosterAthletes.Contains(athlete.Id))
            {
                continue;
            }

            items.Add(new CompetitionTimelineItem(
                $"generated-roster-withdrawn-{athlete.Id}",
                CompetitionUpdateTypes.RosterUpdate,
                athlete.WithdrawnAt ?? athlete.UpdatedAt,
                null,
                athlete.WithdrawalReason,
                athlete.WithdrawalSource,
                [athlete],
                IsGenerated: true));
        }

        return items
            .GroupBy(item => item.Id, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderByDescending(item => item.OccurredAt ?? DateTimeOffset.MinValue)
            .ThenBy(item => SortType(item.Type))
            .ThenBy(item => item.Title ?? item.Athletes.FirstOrDefault()?.DisplayName ?? item.Id, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static CompetitionTimelineItem ToItem(
        CompetitionUpdate update,
        int index,
        IReadOnlyDictionary<string, Athlete> athletesById)
    {
        var type = CompetitionUpdateTypes.Normalize(update.Type);
        var athletes = update.AthleteIds
            .Where(athleteId => !string.IsNullOrWhiteSpace(athleteId))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(athleteId => athletesById.TryGetValue(athleteId, out var athlete) ? athlete : null)
            .OfType<Athlete>()
            .ToArray();

        return new CompetitionTimelineItem(
            BuildId(update, index, type),
            type,
            update.OccurredAt,
            update.Title,
            update.Body,
            update.Source,
            athletes,
            IsGenerated: false);
    }

    private static string BuildId(CompetitionUpdate update, int index, string type)
    {
        if (!string.IsNullOrWhiteSpace(update.Id))
        {
            return update.Id;
        }

        var timestamp = update.OccurredAt?.ToUnixTimeSeconds().ToString() ?? "undated";
        return $"{type}-{timestamp}-{index}";
    }

    private static bool IsRosterUpdate(string? type)
    {
        return string.Equals(
            CompetitionUpdateTypes.Normalize(type),
            CompetitionUpdateTypes.RosterUpdate,
            StringComparison.OrdinalIgnoreCase);
    }

    private static int SortType(string type)
    {
        return CompetitionUpdateTypes.Normalize(type) switch
        {
            CompetitionUpdateTypes.RosterUpdate => 0,
            CompetitionUpdateTypes.DeadlineChange => 1,
            CompetitionUpdateTypes.ResultsUpdate => 2,
            CompetitionUpdateTypes.ScoringUpdate => 3,
            _ => 4
        };
    }
}
