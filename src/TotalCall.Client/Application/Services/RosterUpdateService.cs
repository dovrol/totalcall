using TotalCall.Client.Domain.Athletes;
using TotalCall.Client.Domain.Competitions;
using TotalCall.Client.Domain.Predictions;

namespace TotalCall.Client.Application.Services;

public sealed record RosterAffectedPick(
    string GroupId,
    string QuestionId,
    string AthleteId,
    string AthleteName,
    int? Position);

public static class RosterUpdateService
{
    public static IReadOnlyList<Athlete> GetWithdrawnAthletes(Competition competition)
    {
        return competition.Athletes
            .Where(athlete => athlete.IsWithdrawn)
            .OrderBy(athlete => athlete.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static IReadOnlyList<RosterAffectedPick> FindAffectedPicks(
        Competition competition,
        IEnumerable<PredictionAnswer> answers,
        string? groupId = null,
        string? questionId = null)
    {
        var withdrawnById = competition.Athletes
            .Where(athlete => athlete.IsWithdrawn)
            .ToDictionary(athlete => athlete.Id, StringComparer.OrdinalIgnoreCase);
        if (withdrawnById.Count == 0)
        {
            return [];
        }

        var affected = new List<RosterAffectedPick>();
        foreach (var answer in answers)
        {
            if (!Matches(answer.GroupId, groupId) || !Matches(answer.QuestionId, questionId))
            {
                continue;
            }

            foreach (var selection in GetSelectedAthleteIds(answer))
            {
                if (!withdrawnById.TryGetValue(selection.AthleteId, out var athlete))
                {
                    continue;
                }

                affected.Add(new RosterAffectedPick(
                    answer.GroupId,
                    answer.QuestionId,
                    athlete.Id,
                    athlete.DisplayName,
                    selection.Position));
            }
        }

        return affected
            .GroupBy(
                pick => $"{pick.GroupId}::{pick.QuestionId}::{pick.AthleteId}::{pick.Position}",
                StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(pick => pick.AthleteName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(pick => pick.Position ?? int.MaxValue)
            .ToArray();
    }

    public static IReadOnlyList<string> GetAffectedAthleteNames(
        Competition competition,
        IEnumerable<PredictionAnswer> answers)
    {
        return FindAffectedPicks(competition, answers)
            .Select(pick => pick.AthleteName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IEnumerable<(string AthleteId, int? Position)> GetSelectedAthleteIds(PredictionAnswer answer)
    {
        if (!string.IsNullOrWhiteSpace(answer.Value.SelectedAthleteId))
        {
            yield return (answer.Value.SelectedAthleteId, null);
        }

        foreach (var athleteId in answer.Value.SelectedAthleteIds)
        {
            if (!string.IsNullOrWhiteSpace(athleteId))
            {
                yield return (athleteId, null);
            }
        }

        foreach (var placement in answer.Value.AthletePlacements)
        {
            if (placement.IsScored && !string.IsNullOrWhiteSpace(placement.AthleteId))
            {
                yield return (placement.AthleteId, placement.Position);
            }
        }
    }

    private static bool Matches(string value, string? filter)
    {
        return string.IsNullOrWhiteSpace(filter) ||
               string.Equals(value, filter, StringComparison.OrdinalIgnoreCase);
    }
}
