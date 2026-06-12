using TotalCall.Client.Domain.Competitions;
using TotalCall.Client.Domain.Predictions;

namespace TotalCall.Sync.Results;

public sealed class OfficialResultsValidator
{
    public IReadOnlyList<string> Validate(
        Competition competition,
        OfficialResultsFile resultsFile,
        string expectedCompetitionId)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(resultsFile.CompetitionId))
        {
            errors.Add("results.competitionId is required.");
        }
        else if (!string.Equals(resultsFile.CompetitionId, expectedCompetitionId, StringComparison.OrdinalIgnoreCase))
        {
            errors.Add(
                $"results.competitionId '{resultsFile.CompetitionId}' does not match '{expectedCompetitionId}'.");
        }

        if (!IsImportStatus(resultsFile.Status))
        {
            errors.Add("results.status must be 'partial' or 'final'.");
        }

        var groupById = competition.PredictionGroups.ToDictionary(
            group => group.Id,
            StringComparer.OrdinalIgnoreCase);
        var categoryIds = competition.Categories
            .Select(category => category.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var athleteIds = competition.Athletes
            .Select(athlete => athlete.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var resultGroup in resultsFile.Groups)
        {
            ValidateGroup(resultGroup, groupById, categoryIds, athleteIds, errors);
        }

        return errors;
    }

    private static void ValidateGroup(
        OfficialResultGroupFile resultGroup,
        IReadOnlyDictionary<string, PredictionGroup> groupById,
        ISet<string> categoryIds,
        ISet<string> athleteIds,
        List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(resultGroup.GroupId))
        {
            errors.Add("results.groups[].groupId is required.");
            return;
        }

        if (string.IsNullOrWhiteSpace(resultGroup.QuestionId))
        {
            errors.Add($"results.groups[{resultGroup.GroupId}].questionId is required.");
            return;
        }

        if (!IsGroupStatus(resultGroup.Status))
        {
            errors.Add(
                $"results.groups[{resultGroup.GroupId}/{resultGroup.QuestionId}].status must be 'pending' or 'final'.");
        }

        if (!groupById.TryGetValue(resultGroup.GroupId, out var predictionGroup))
        {
            errors.Add($"Unknown prediction group_id '{resultGroup.GroupId}'.");
            return;
        }

        var question = predictionGroup.Questions.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, resultGroup.QuestionId, StringComparison.OrdinalIgnoreCase));
        if (question is null)
        {
            errors.Add(
                $"Unknown question_id '{resultGroup.QuestionId}' in group_id '{resultGroup.GroupId}'.");
            return;
        }

        if (!string.IsNullOrWhiteSpace(resultGroup.CategoryId) &&
            !categoryIds.Contains(resultGroup.CategoryId))
        {
            errors.Add(
                $"Unknown category_id '{resultGroup.CategoryId}' for '{resultGroup.GroupId}/{resultGroup.QuestionId}'.");
        }

        if (!string.IsNullOrWhiteSpace(question.CategoryId) &&
            !string.IsNullOrWhiteSpace(resultGroup.CategoryId) &&
            !string.Equals(question.CategoryId, resultGroup.CategoryId, StringComparison.OrdinalIgnoreCase))
        {
            errors.Add(
                $"category_id '{resultGroup.CategoryId}' does not match config category_id '{question.CategoryId}' " +
                $"for '{resultGroup.GroupId}/{resultGroup.QuestionId}'.");
        }

        if (string.Equals(resultGroup.Status, OfficialResultGroupImportStatus.Final, StringComparison.OrdinalIgnoreCase) &&
            resultGroup.Placements.Count == 0)
        {
            errors.Add($"Final result group '{resultGroup.GroupId}/{resultGroup.QuestionId}' must include placements.");
        }

        ValidatePlacements(resultGroup, question, athleteIds, errors);
    }

    private static void ValidatePlacements(
        OfficialResultGroupFile resultGroup,
        PredictionQuestion question,
        ISet<string> athleteIds,
        List<string> errors)
    {
        var seenPositions = new HashSet<int>();
        var seenAthletes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var allowedAthleteIds = question.AthleteIds.Count > 0
            ? question.AthleteIds.ToHashSet(StringComparer.OrdinalIgnoreCase)
            : null;

        foreach (var placement in resultGroup.Placements)
        {
            var path = $"results.groups[{resultGroup.GroupId}/{resultGroup.QuestionId}].placements";
            if (placement.Position <= 0)
            {
                errors.Add($"{path} position must be positive.");
            }

            if (!seenPositions.Add(placement.Position))
            {
                errors.Add($"{path} contains duplicate position {placement.Position}.");
            }

            if (string.IsNullOrWhiteSpace(placement.AthleteId))
            {
                errors.Add($"{path}[{placement.Position}].athleteId is required.");
                continue;
            }

            if (!seenAthletes.Add(placement.AthleteId))
            {
                errors.Add($"{path} contains duplicate athlete_id '{placement.AthleteId}'.");
            }

            if (!athleteIds.Contains(placement.AthleteId))
            {
                errors.Add($"{path}[{placement.Position}] references unknown athlete_id '{placement.AthleteId}'.");
            }

            if (allowedAthleteIds is not null && !allowedAthleteIds.Contains(placement.AthleteId))
            {
                errors.Add(
                    $"{path}[{placement.Position}] athlete_id '{placement.AthleteId}' is not allowed by the question config.");
            }
        }
    }

    private static bool IsImportStatus(string? status) =>
        string.Equals(status, OfficialResultImportStatus.Partial, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(status, OfficialResultImportStatus.Final, StringComparison.OrdinalIgnoreCase);

    private static bool IsGroupStatus(string? status) =>
        string.Equals(status, OfficialResultGroupImportStatus.Pending, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(status, OfficialResultGroupImportStatus.Final, StringComparison.OrdinalIgnoreCase);
}
