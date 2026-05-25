using TotalCall.Client.Domain.Athletes;
using TotalCall.Client.Domain.Competitions;
using TotalCall.Client.Domain.Predictions;
using TotalCall.Client.Domain.Predictions.Review;
using Microsoft.Extensions.Localization;

namespace TotalCall.Client.Application.Services.Review;

public sealed class CompetitionReviewBuilder(
    IPredictionValidationService validationService,
    PredictionTextService text,
    PredictionAnswerDisplayService answerDisplay)
{
    public CompetitionReviewModel Build(Competition competition, PredictionSet predictionSet, bool canEditPredictions)
    {
        var orderedGroups = competition.PredictionGroups
            .OrderBy(group => group.Order)
            .ToArray();

        var modules = new List<PredictionModuleReviewModel>(orderedGroups.Length);
        var totalSections = 0;
        var completedSections = 0;
        var savedPicks = 0;
        var completedModules = 0;

        foreach (var group in orderedGroups)
        {
            var moduleValidation = validationService.ValidateModule(competition, group, predictionSet);
            var moduleType = ResolveGroupType(group);
            var moduleSections = BuildSections(
                competition,
                group,
                moduleType,
                moduleValidation,
                predictionSet);

            var moduleSavedPicks = moduleSections.Sum(section => section.SelectedCount);
            var hasGroupedSections = moduleSections.Any(section => !string.IsNullOrWhiteSpace(section.GroupLabel));

            modules.Add(new PredictionModuleReviewModel
            {
                GroupId = group.Id,
                ModuleType = moduleType,
                Title = text.GroupTitle(competition, group),
                Description = NullIfEmpty(text.GroupDescription(competition, group)),
                Status = moduleValidation.Status,
                TotalSections = moduleValidation.TotalItems,
                CompletedSections = moduleValidation.CompletedItems,
                SavedPicks = moduleSavedPicks,
                Order = group.Order,
                EditHref = $"competitions/{competition.Slug}/predictions",
                HasGroupedSections = hasGroupedSections,
                Sections = moduleSections
            });

            totalSections += moduleValidation.TotalItems;
            completedSections += moduleValidation.CompletedItems;
            savedPicks += moduleSavedPicks;
            if (moduleValidation.Status == PredictionCompletionStatus.Complete)
            {
                completedModules++;
            }
        }

        return new CompetitionReviewModel
        {
            CompetitionId = competition.Id,
            CompetitionSlug = competition.Slug,
            CompetitionName = competition.Name,
            PredictionLockAt = competition.PredictionLockAt,
            SavedAt = predictionSet.SavedAt,
            CanEditPredictions = canEditPredictions,
            Stats = new ReviewCompletionStats(
                completedModules,
                orderedGroups.Length,
                completedSections,
                totalSections,
                savedPicks),
            Modules = modules
        };
    }

    private IReadOnlyList<ReviewSectionModel> BuildSections(
        Competition competition,
        PredictionGroup group,
        string moduleType,
        PredictionModuleValidationResult moduleValidation,
        PredictionSet predictionSet)
    {
        var statusByQuestion = moduleValidation.Questions
            .ToDictionary(result => result.QuestionId, StringComparer.OrdinalIgnoreCase);

        var orderedQuestions = group.Questions
            .OrderBy(question => question.Order)
            .ToArray();

        var sections = new List<ReviewSectionModel>(orderedQuestions.Length);

        foreach (var question in orderedQuestions)
        {
            var questionStatus = statusByQuestion.TryGetValue(question.Id, out var status)
                ? status
                : new PredictionQuestionCompletionResult(question.Id, PredictionCompletionStatus.NotStarted, 0, 0);

            var answer = predictionSet.Answers.FirstOrDefault(candidate =>
                string.Equals(candidate.GroupId, group.Id, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(candidate.QuestionId, question.Id, StringComparison.OrdinalIgnoreCase));

            var isRankingLayout =
                moduleType == PredictionModuleType.TopNByCategory ||
                question.Type is PredictionQuestionType.AthleteRanking
                    or PredictionQuestionType.CategoryPodium;

            var layout = isRankingLayout
                ? ReviewSectionLayout.AthleteRanking
                : ReviewSectionLayout.SimpleSummary;

            var (title, groupLabel) = ResolveSectionTitle(competition, question, moduleType);

            IReadOnlyList<ReviewPickRowModel> picks = [];
            string? summaryText = null;

            if (layout == ReviewSectionLayout.AthleteRanking)
            {
                picks = BuildPickRows(competition, question, answer);
            }
            else
            {
                summaryText = answerDisplay.FormatAnswer(competition, question, answer);
            }

            sections.Add(new ReviewSectionModel
            {
                Id = $"{group.Id}::{question.Id}",
                QuestionId = question.Id,
                Title = title,
                GroupLabel = groupLabel,
                Status = questionStatus.Status,
                SelectedCount = questionStatus.SelectedCount,
                RequiredCount = Math.Max(questionStatus.RequiredCount, 1),
                Layout = layout,
                Picks = picks,
                SummaryText = summaryText,
                EditHref = $"competitions/{competition.Slug}/predictions"
            });
        }

        return sections;
    }

    private static (string Title, string? GroupLabel) ResolveSectionTitle(
        Competition competition,
        PredictionQuestion question,
        string moduleType)
    {
        if (moduleType == PredictionModuleType.TopNByCategory && !string.IsNullOrWhiteSpace(question.CategoryId))
        {
            var category = competition.Categories.FirstOrDefault(item => item.Id == question.CategoryId);
            if (category is not null)
            {
                var groupLabel = category.Sex switch
                {
                    AthleteSex.Female => "women",
                    AthleteSex.Male => "men",
                    _ => null
                };
                return (category.Name, groupLabel);
            }
        }

        return (question.Title, null);
    }

    private IReadOnlyList<ReviewPickRowModel> BuildPickRows(
        Competition competition,
        PredictionQuestion question,
        PredictionAnswer? answer)
    {
        var slotsCount = question.Constraints.ExactSelections
            ?? question.Constraints.MaxSelections
            ?? Math.Max(answer?.Value.AthletePlacements.Count ?? 0, 1);

        var placementsByPosition = (answer?.Value.AthletePlacements ?? [])
            .Where(placement => placement.Position >= 1)
            .GroupBy(placement => placement.Position)
            .ToDictionary(group => group.Key, group => group.First());

        var rows = new List<ReviewPickRowModel>(slotsCount);

        for (var position = 1; position <= slotsCount; position++)
        {
            if (placementsByPosition.TryGetValue(position, out var placement) &&
                !string.IsNullOrWhiteSpace(placement.AthleteId))
            {
                var athlete = competition.Athletes.FirstOrDefault(item =>
                    string.Equals(item.Id, placement.AthleteId, StringComparison.OrdinalIgnoreCase));

                rows.Add(new ReviewPickRowModel
                {
                    Position = position,
                    AthleteId = placement.AthleteId,
                    AthleteName = athlete?.DisplayName ?? placement.AthleteId,
                    CountryCode = athlete?.CountryCode,
                    CountryName = athlete?.CountryName,
                    PredictedTotalKg = placement.PredictedTotalKg,
                    PredictedSquatKg = placement.PredictedSquatKg,
                    PredictedBenchKg = placement.PredictedBenchKg,
                    PredictedDeadliftKg = placement.PredictedDeadliftKg
                });
            }
            else
            {
                rows.Add(new ReviewPickRowModel
                {
                    Position = position,
                    AthleteId = string.Empty,
                    AthleteName = string.Empty
                });
            }
        }

        return rows;
    }

    private static string ResolveGroupType(PredictionGroup group)
    {
        var normalizedType = PredictionModuleType.Normalize(group.Type);
        if (!string.IsNullOrWhiteSpace(normalizedType))
        {
            return normalizedType;
        }

        var questionTypes = group.Questions
            .Select(question => question.Type)
            .Distinct()
            .ToArray();

        if (questionTypes.Length == 1 &&
            questionTypes[0] == PredictionQuestionType.AthleteRanking &&
            group.Questions.Count > 1 &&
            group.Questions.All(question => !string.IsNullOrWhiteSpace(question.CategoryId)))
        {
            return PredictionModuleType.TopNByCategory;
        }

        return string.Empty;
    }

    private static string? NullIfEmpty(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

}
