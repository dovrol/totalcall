using TotalCall.Client.Domain.Competitions;
using TotalCall.Client.Domain.Predictions;
using TotalCall.Client.Storage;

namespace TotalCall.Client.Application.Services;

public sealed class PredictionService(
    IPredictionStore predictionStore,
    AppInfoService appInfo)
{
    public async Task<PredictionSet> GetOrCreatePredictionSetAsync(
        Competition competition,
        CancellationToken cancellationToken = default)
    {
        var savedPredictions = await predictionStore.GetAsync(competition.Id, cancellationToken);

        if (savedPredictions is not null &&
            savedPredictions.CompetitionConfigVersion == competition.ConfigVersion)
        {
            return EnsureStorageMetadata(savedPredictions);
        }

        return new PredictionSet
        {
            CompetitionId = competition.Id,
            CompetitionConfigVersion = competition.ConfigVersion,
            AppVersion = appInfo.AppVersion,
            SchemaVersion = PredictionSet.StorageSchemaVersion,
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
            AppVersion = appInfo.AppVersion,
            SchemaVersion = PredictionSet.StorageSchemaVersion,
            SavedAt = DateTimeOffset.UtcNow
        };
    }

    public PredictionSet CreateExportPayload(PredictionSet predictionSet)
    {
        return predictionSet with
        {
            AppVersion = appInfo.AppVersion,
            SchemaVersion = PredictionSet.StorageSchemaVersion,
            SavedAt = DateTimeOffset.UtcNow
        };
    }

    public Task SaveAsync(PredictionSet predictionSet, CancellationToken cancellationToken = default)
    {
        return predictionStore.SaveAsync(
            CreateExportPayload(predictionSet),
            cancellationToken);
    }

    private PredictionSet EnsureStorageMetadata(PredictionSet predictionSet)
    {
        return predictionSet with
        {
            AppVersion = string.IsNullOrWhiteSpace(predictionSet.AppVersion)
                ? appInfo.AppVersion
                : predictionSet.AppVersion,
            SchemaVersion = predictionSet.SchemaVersion > 0
                ? predictionSet.SchemaVersion
                : PredictionSet.StorageSchemaVersion
        };
    }
}
