using TotalCall.Client.Domain.Competitions;
using TotalCall.Client.Domain.Predictions;
using TotalCall.Client.Storage;

namespace TotalCall.Client.Application.Services;

public sealed class PredictionService(IPredictionStore predictionStore)
{
    public async Task<PredictionSet> GetOrCreatePredictionSetAsync(
        Competition competition,
        CancellationToken cancellationToken = default)
    {
        var savedPredictions = await predictionStore.GetAsync(competition.Id, cancellationToken);

        if (savedPredictions is not null &&
            savedPredictions.CompetitionConfigVersion == competition.ConfigVersion)
        {
            return savedPredictions;
        }

        return new PredictionSet
        {
            CompetitionId = competition.Id,
            CompetitionConfigVersion = competition.ConfigVersion,
            SavedAt = DateTimeOffset.UtcNow
        };
    }

    public PredictionSet UpdateAnswer(PredictionSet predictionSet, PredictionAnswer answer)
    {
        var answers = predictionSet.Answers
            .Where(existingAnswer => existingAnswer.QuestionId != answer.QuestionId)
            .Append(answer)
            .OrderBy(existingAnswer => existingAnswer.GroupId)
            .ThenBy(existingAnswer => existingAnswer.QuestionId)
            .ToArray();

        return predictionSet with
        {
            Answers = answers,
            SavedAt = DateTimeOffset.UtcNow
        };
    }

    public Task SaveAsync(PredictionSet predictionSet, CancellationToken cancellationToken = default)
    {
        return predictionStore.SaveAsync(
            predictionSet with { SavedAt = DateTimeOffset.UtcNow },
            cancellationToken);
    }
}
