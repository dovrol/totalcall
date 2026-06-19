using TotalCall.Core.Domain.Athletes;
using TotalCall.Core.Domain.Competitions;
using TotalCall.Core.Domain.Predictions;

namespace TotalCall.Core.Validation;

public sealed class CompetitionConfigValidator
{
    private const string TopNFullMode = "full";

    private static readonly string[] KnownModuleTypes =
    [
        PredictionModuleType.TopNByCategory,
        PredictionModuleType.AthleteRanking,
        PredictionModuleType.CategoryPodium,
        PredictionModuleType.NumericAthletePrediction,
        PredictionModuleType.SingleAthleteChoice,
        PredictionModuleType.MultiAthleteChoice,
        PredictionModuleType.YesNo,
        PredictionModuleType.MultipleChoice,
        PredictionModuleType.NumericQuestion
    ];

    public CompetitionConfigValidationResult Validate(
        Competition competition,
        CompetitionConfigValidationOptions? options = null)
    {
        options ??= CompetitionConfigValidationOptions.Production;

        var errors = new List<CompetitionConfigValidationError>();
        var supportedModuleTypes = options.SupportedModuleTypes
            .Select(PredictionModuleType.Normalize)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        ValidateCompetitionFields(competition, options, errors);

        var athleteIds = competition.Athletes
            .Where(athlete => !string.IsNullOrWhiteSpace(athlete.Id))
            .Select(athlete => athlete.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var categoriesById = competition.Categories
            .Where(category => !string.IsNullOrWhiteSpace(category.Id))
            .GroupBy(category => category.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
        var athletesById = competition.Athletes
            .Where(athlete => !string.IsNullOrWhiteSpace(athlete.Id))
            .GroupBy(athlete => athlete.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        ValidateAthletes(competition, categoriesById.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase), errors);
        ValidateCategories(competition, athleteIds, errors);
        ValidateUpdates(competition, athleteIds, errors);
        ValidatePredictionGroups(competition, options, supportedModuleTypes, athletesById, categoriesById, errors);

        return new CompetitionConfigValidationResult(errors);
    }

    private static void ValidateCompetitionFields(
        Competition competition,
        CompetitionConfigValidationOptions options,
        List<CompetitionConfigValidationError> errors)
    {
        RequireText(competition.Id, "id", "CompetitionIdRequired", "Competition id is required.", errors);
        RequireText(competition.Slug, "slug", "CompetitionSlugRequired", "Competition slug is required.", errors);
        RequireText(competition.Name, "name", "CompetitionNameRequired", "Competition name is required.", errors);
        RequireText(
            competition.ConfigVersion,
            "configVersion",
            "CompetitionConfigVersionRequired",
            "Competition configVersion is required.",
            errors);

        if (!options.RequireLifecycleFields)
        {
            return;
        }

        if (competition.StartDate is null)
        {
            AddError(errors, "startDate", "CompetitionStartDateRequired", "Competition startDate is required.");
        }

        if (competition.EndDate is null)
        {
            AddError(errors, "endDate", "CompetitionEndDateRequired", "Competition endDate is required.");
        }

        if (competition.PredictionOpenAt is null)
        {
            AddError(
                errors,
                "predictionOpenAt",
                "PredictionOpenAtRequired",
                "Competition predictionOpenAt is required.");
        }

        if (competition.PredictionLockAt is null)
        {
            AddError(
                errors,
                "predictionLockAt",
                "PredictionLockAtRequired",
                "Competition predictionLockAt is required.");
        }

        if (competition.StartDate is { } start && competition.EndDate is { } end && end < start)
        {
            AddError(errors, "endDate", "CompetitionEndBeforeStart", "Competition endDate cannot be before startDate.");
        }

        if (competition.PredictionOpenAt is { } openAt &&
            competition.PredictionLockAt is { } lockAt &&
            openAt >= lockAt)
        {
            AddError(
                errors,
                "predictionLockAt",
                "PredictionLockBeforeOpen",
                "Competition predictionLockAt must be after predictionOpenAt.");
        }

        if (competition.PredictionLockAt is { } predictionLockAt &&
            competition.StartDate is { } competitionStart &&
            predictionLockAt > competitionStart)
        {
            AddError(
                errors,
                "predictionLockAt",
                "PredictionLockAfterStart",
                "Competition predictionLockAt cannot be after startDate.");
        }
    }

    private static void ValidateAthletes(
        Competition competition,
        ISet<string> categoryIds,
        List<CompetitionConfigValidationError> errors)
    {
        if (competition.Athletes.Count == 0)
        {
            AddError(errors, "athletes", "AthletesRequired", "Competition must define at least one athlete.");
            return;
        }

        ValidateUniqueIds(
            competition.Athletes,
            "athletes",
            athlete => athlete.Id,
            "DuplicateAthleteId",
            errors);

        for (var index = 0; index < competition.Athletes.Count; index++)
        {
            var athlete = competition.Athletes[index];
            var path = $"athletes[{index}]";
            RequireText(athlete.Id, $"{path}.id", "AthleteIdRequired", "Athlete id is required.", errors);
            RequireText(
                athlete.DisplayName,
                $"{path}.displayName",
                "AthleteDisplayNameRequired",
                "Athlete displayName is required.",
                errors);

            if (!string.IsNullOrWhiteSpace(athlete.WeightCategoryId) &&
                !categoryIds.Contains(athlete.WeightCategoryId))
            {
                AddError(
                    errors,
                    $"{path}.weightCategoryId",
                    "UnknownAthleteCategory",
                    $"Athlete '{athlete.Id}' references unknown category '{athlete.WeightCategoryId}'.");
            }
        }
    }

    private static void ValidateCategories(
        Competition competition,
        ISet<string> athleteIds,
        List<CompetitionConfigValidationError> errors)
    {
        if (competition.Categories.Count == 0)
        {
            AddError(errors, "categories", "CategoriesRequired", "Competition must define at least one category.");
            return;
        }

        ValidateUniqueIds(
            competition.Categories,
            "categories",
            category => category.Id,
            "DuplicateCategoryId",
            errors);

        for (var index = 0; index < competition.Categories.Count; index++)
        {
            var category = competition.Categories[index];
            var path = $"categories[{index}]";
            RequireText(category.Id, $"{path}.id", "CategoryIdRequired", "Category id is required.", errors);
            RequireText(category.Name, $"{path}.name", "CategoryNameRequired", "Category name is required.", errors);
            ValidateDuplicateStrings(
                category.AthleteIds,
                $"{path}.athleteIds",
                "DuplicateCategoryAthleteId",
                errors);

            foreach (var athleteId in category.AthleteIds)
            {
                if (!athleteIds.Contains(athleteId))
                {
                    AddError(
                        errors,
                        $"{path}.athleteIds",
                        "UnknownCategoryAthlete",
                        $"Category '{category.Id}' references unknown athlete '{athleteId}'.");
                }
            }
        }
    }

    private static void ValidateUpdates(
        Competition competition,
        ISet<string> athleteIds,
        List<CompetitionConfigValidationError> errors)
    {
        ValidateUniqueIds(
            competition.Updates.Where(update => !string.IsNullOrWhiteSpace(update.Id)).ToArray(),
            "updates",
            update => update.Id,
            "DuplicateUpdateId",
            errors);

        for (var index = 0; index < competition.Updates.Count; index++)
        {
            var update = competition.Updates[index];
            var path = $"updates[{index}]";
            ValidateDuplicateStrings(update.AthleteIds, $"{path}.athlete_ids", "DuplicateUpdateAthleteId", errors);

            foreach (var athleteId in update.AthleteIds)
            {
                if (!athleteIds.Contains(athleteId))
                {
                    AddError(
                        errors,
                        $"{path}.athlete_ids",
                        "UnknownUpdateAthlete",
                        $"Competition update '{update.Id ?? index.ToString()}' references unknown athlete '{athleteId}'.");
                }
            }
        }
    }

    private static void ValidatePredictionGroups(
        Competition competition,
        CompetitionConfigValidationOptions options,
        ISet<string> supportedModuleTypes,
        IReadOnlyDictionary<string, Athlete> athletesById,
        IReadOnlyDictionary<string, WeightCategory> categoriesById,
        List<CompetitionConfigValidationError> errors)
    {
        if (competition.PredictionGroups.Count == 0)
        {
            AddError(
                errors,
                "predictionGroups",
                "PredictionGroupsRequired",
                "Competition must define at least one prediction group.");
            return;
        }

        ValidateUniqueIds(
            competition.PredictionGroups,
            "predictionGroups",
            group => group.Id,
            "DuplicatePredictionGroupId",
            errors);

        var questionIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var groupIndex = 0; groupIndex < competition.PredictionGroups.Count; groupIndex++)
        {
            var group = competition.PredictionGroups[groupIndex];
            var groupPath = $"predictionGroups[{groupIndex}]";
            var normalizedType = PredictionModuleType.Normalize(group.Type);

            RequireText(group.Id, $"{groupPath}.id", "PredictionGroupIdRequired", "Prediction group id is required.", errors);
            RequireText(
                group.Title,
                $"{groupPath}.title",
                "PredictionGroupTitleRequired",
                "Prediction group title is required.",
                errors);

            if (string.IsNullOrWhiteSpace(normalizedType))
            {
                AddError(
                    errors,
                    $"{groupPath}.type",
                    "PredictionGroupTypeRequired",
                    $"Prediction group '{group.Id}' must declare an explicit type.");
            }
            else
            {
                ValidateModuleType(group, groupPath, normalizedType, supportedModuleTypes, errors);
            }

            if (group.Questions.Count == 0)
            {
                AddError(
                    errors,
                    $"{groupPath}.questions",
                    "PredictionGroupQuestionsRequired",
                    $"Prediction group '{group.Id}' must define at least one question.");
                continue;
            }

            ValidateUniqueIds(
                group.Questions,
                $"{groupPath}.questions",
                question => question.Id,
                "DuplicateQuestionIdInGroup",
                errors);

            foreach (var question in group.Questions)
            {
                if (!string.IsNullOrWhiteSpace(question.Id) && !questionIds.Add(question.Id))
                {
                    AddError(
                        errors,
                        $"{groupPath}.questions",
                        "DuplicateQuestionId",
                        $"Question id '{question.Id}' is duplicated across prediction groups.");
                }
            }

            if (normalizedType == PredictionModuleType.TopNByCategory)
            {
                ValidateTopNGroup(group, groupPath, options, athletesById, categoriesById, errors);
            }
        }
    }

    private static void ValidateModuleType(
        PredictionGroup group,
        string groupPath,
        string normalizedType,
        ISet<string> supportedModuleTypes,
        List<CompetitionConfigValidationError> errors)
    {
        if (!KnownModuleTypes.Contains(normalizedType, StringComparer.OrdinalIgnoreCase))
        {
            AddError(
                errors,
                $"{groupPath}.type",
                "UnknownModuleType",
                $"Prediction group '{group.Id}' uses unknown module type '{group.Type}'.");
            return;
        }

        if (!supportedModuleTypes.Contains(normalizedType))
        {
            AddError(
                errors,
                $"{groupPath}.type",
                "UnsupportedModuleType",
                $"Prediction group '{group.Id}' uses module type '{group.Type}', which is not supported for production config.");
        }
    }

    private static void ValidateTopNGroup(
        PredictionGroup group,
        string groupPath,
        CompetitionConfigValidationOptions options,
        IReadOnlyDictionary<string, Athlete> athletesById,
        IReadOnlyDictionary<string, WeightCategory> categoriesById,
        List<CompetitionConfigValidationError> errors)
    {
        if (!string.Equals(group.Mode, TopNFullMode, StringComparison.OrdinalIgnoreCase))
        {
            AddError(
                errors,
                $"{groupPath}.mode",
                "TopNModeUnsupported",
                $"Top N group '{group.Id}' must use mode '{TopNFullMode}'.");
        }

        for (var questionIndex = 0; questionIndex < group.Questions.Count; questionIndex++)
        {
            var question = group.Questions[questionIndex];
            var questionPath = $"{groupPath}.questions[{questionIndex}]";

            RequireText(question.Id, $"{questionPath}.id", "QuestionIdRequired", "Question id is required.", errors);
            RequireText(question.Title, $"{questionPath}.title", "QuestionTitleRequired", "Question title is required.", errors);

            if (question.Type != PredictionQuestionType.AthleteRanking)
            {
                AddError(
                    errors,
                    $"{questionPath}.type",
                    "TopNQuestionTypeUnsupported",
                    $"Top N question '{question.Id}' must use type 'athlete-ranking'.");
            }

            if (string.IsNullOrWhiteSpace(question.CategoryId))
            {
                AddError(
                    errors,
                    $"{questionPath}.categoryId",
                    "TopNQuestionCategoryRequired",
                    $"Top N question '{question.Id}' must reference a category.");
            }
            else if (!categoriesById.TryGetValue(question.CategoryId, out var category))
            {
                AddError(
                    errors,
                    $"{questionPath}.categoryId",
                    "UnknownQuestionCategory",
                    $"Top N question '{question.Id}' references unknown category '{question.CategoryId}'.");
            }
            else
            {
                ValidateTopNAthletesAgainstCategory(question, questionPath, category, athletesById, errors);
            }

            if (!question.Required)
            {
                AddError(
                    errors,
                    $"{questionPath}.required",
                    "TopNQuestionRequired",
                    $"Top N question '{question.Id}' must be required.");
            }

            if (question.Constraints.ExactSelections != options.TopNExactSelections)
            {
                AddError(
                    errors,
                    $"{questionPath}.constraints.exactSelections",
                    "TopNExactSelectionsRequired",
                    $"Top N question '{question.Id}' must set exactSelections to {options.TopNExactSelections}.");
            }

            if (!question.Constraints.DisallowDuplicateAthletes)
            {
                AddError(
                    errors,
                    $"{questionPath}.constraints.disallowDuplicateAthletes",
                    "TopNDuplicateAthletesMustBeDisallowed",
                    $"Top N question '{question.Id}' must disallow duplicate athletes.");
            }

            if (question.AthleteIds.Count < options.TopNExactSelections)
            {
                AddError(
                    errors,
                    $"{questionPath}.athleteIds",
                    "TopNFieldTooSmall",
                    $"Top N question '{question.Id}' must include at least {options.TopNExactSelections} athletes.");
            }

            ValidateDuplicateStrings(
                question.AthleteIds,
                $"{questionPath}.athleteIds",
                "DuplicateQuestionAthleteId",
                errors);
        }
    }

    private static void ValidateTopNAthletesAgainstCategory(
        PredictionQuestion question,
        string questionPath,
        WeightCategory category,
        IReadOnlyDictionary<string, Athlete> athletesById,
        List<CompetitionConfigValidationError> errors)
    {
        var categoryAthleteIds = category.AthleteIds.ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var athleteId in question.AthleteIds)
        {
            if (!athletesById.TryGetValue(athleteId, out var athlete))
            {
                AddError(
                    errors,
                    $"{questionPath}.athleteIds",
                    "UnknownQuestionAthlete",
                    $"Question '{question.Id}' references unknown athlete '{athleteId}'.");
                continue;
            }

            if (categoryAthleteIds.Count > 0 && !categoryAthleteIds.Contains(athleteId))
            {
                AddError(
                    errors,
                    $"{questionPath}.athleteIds",
                    "QuestionAthleteNotInCategory",
                    $"Question '{question.Id}' athlete '{athleteId}' is not listed in category '{category.Id}'.");
            }

            if (!string.IsNullOrWhiteSpace(athlete.WeightCategoryId) &&
                !string.Equals(athlete.WeightCategoryId, category.Id, StringComparison.OrdinalIgnoreCase))
            {
                AddError(
                    errors,
                    $"{questionPath}.athleteIds",
                    "QuestionAthleteCategoryMismatch",
                    $"Question '{question.Id}' athlete '{athleteId}' has category '{athlete.WeightCategoryId}', expected '{category.Id}'.");
            }
        }
    }

    private static void RequireText(
        string? value,
        string path,
        string code,
        string message,
        List<CompetitionConfigValidationError> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            AddError(errors, path, code, message);
        }
    }

    private static void ValidateUniqueIds<T>(
        IReadOnlyList<T> items,
        string path,
        Func<T, string?> idSelector,
        string code,
        List<CompetitionConfigValidationError> errors)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in items)
        {
            var id = idSelector(item);
            if (string.IsNullOrWhiteSpace(id))
            {
                continue;
            }

            if (!seen.Add(id))
            {
                AddError(errors, path, code, $"{path} contains duplicate id '{id}'.");
            }
        }
    }

    private static void ValidateDuplicateStrings(
        IReadOnlyList<string> values,
        string path,
        string code,
        List<CompetitionConfigValidationError> errors)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var value in values)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                AddError(errors, path, code, $"{path} contains an empty value.");
                continue;
            }

            if (!seen.Add(value))
            {
                AddError(errors, path, code, $"{path} contains duplicate value '{value}'.");
            }
        }
    }

    private static void AddError(
        List<CompetitionConfigValidationError> errors,
        string path,
        string code,
        string message)
    {
        errors.Add(new CompetitionConfigValidationError(path, code, message));
    }
}

public sealed record CompetitionConfigValidationOptions
{
    public static CompetitionConfigValidationOptions Production { get; } = new();

    public IReadOnlyCollection<string> SupportedModuleTypes { get; init; } =
        [PredictionModuleType.TopNByCategory];

    public bool RequireLifecycleFields { get; init; } = true;

    public int TopNExactSelections { get; init; } = 3;
}

public sealed record CompetitionConfigValidationResult(
    IReadOnlyList<CompetitionConfigValidationError> Errors)
{
    public bool IsValid => Errors.Count == 0;
}

public sealed record CompetitionConfigValidationError(
    string Path,
    string Code,
    string Message);
