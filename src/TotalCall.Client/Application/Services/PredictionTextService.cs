using Microsoft.Extensions.Localization;
using TotalCall.Core.Domain.Competitions;
using TotalCall.Core.Domain.Predictions;

namespace TotalCall.Client.Application.Services;

public sealed class PredictionTextService(IStringLocalizer<SharedResource> localizer)
{
    public string CompetitionDescription(CompetitionSummary competition)
    {
        return LocalizedOrFallback($"Data.Competition.{competition.Slug}.Description", competition.Description);
    }

    public string CompetitionDescription(Competition competition)
    {
        return LocalizedOrFallback($"Data.Competition.{competition.Slug}.Description", competition.Description);
    }

    public string GroupTitle(Competition competition, PredictionGroup group)
    {
        return LocalizedOrFallback($"Data.Group.{competition.Slug}.{group.Id}.Title", group.Title);
    }

    public string? GroupDescription(Competition competition, PredictionGroup group)
    {
        return LocalizedOrFallback($"Data.Group.{competition.Slug}.{group.Id}.Description", group.Description);
    }

    public string QuestionTitle(Competition competition, PredictionQuestion question)
    {
        return LocalizedOrFallback($"Data.Question.{competition.Slug}.{question.Id}.Title", question.Title);
    }

    public string? QuestionDescription(Competition competition, PredictionQuestion question)
    {
        return LocalizedOrFallback($"Data.Question.{competition.Slug}.{question.Id}.Description", question.Description);
    }

    public string OptionLabel(Competition competition, PredictionQuestion question, PredictionOption option)
    {
        return LocalizedOrFallback(
            $"Data.Option.{competition.Slug}.{question.Id}.{option.Id}.Label",
            option.Label);
    }

    private string LocalizedOrFallback(string key, string? fallback)
    {
        var localized = localizer[key];

        return localized.ResourceNotFound
            ? fallback ?? string.Empty
            : localized.Value;
    }
}
