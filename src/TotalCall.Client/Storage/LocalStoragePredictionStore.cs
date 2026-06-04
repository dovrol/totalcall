using System.Text.Json;
using TotalCall.Client.Domain.Predictions;
using TotalCall.Client.Infrastructure.Browser;
using TotalCall.Client.Infrastructure.Json;

namespace TotalCall.Client.Storage;

public sealed class LocalStoragePredictionStore(BrowserLocalStorage localStorage) : IPredictionStore
{
    public async Task<PredictionSet?> GetAsync(string competitionId, CancellationToken cancellationToken = default)
    {
        var json = await localStorage.GetItemAsync(LocalStorageKeys.Predictions(competitionId), cancellationToken);

        return string.IsNullOrWhiteSpace(json)
            ? null
            : JsonSerializer.Deserialize<PredictionSet>(json, JsonDataOptions.SerializerOptions);
    }

    public async Task SaveAsync(PredictionSet predictionSet, CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(predictionSet, JsonDataOptions.SerializerOptions);

        await localStorage.SetItemAsync(
            LocalStorageKeys.Predictions(predictionSet.CompetitionId),
            json,
            cancellationToken);
    }

    public async Task<IReadOnlyList<PredictionSet>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var keys = await localStorage.GetKeysAsync(LocalStorageKeys.PredictionsPrefix, cancellationToken);
        var predictions = new List<PredictionSet>(keys.Length);

        foreach (var key in keys)
        {
            var json = await localStorage.GetItemAsync(key, cancellationToken);
            if (string.IsNullOrWhiteSpace(json))
            {
                continue;
            }

            try
            {
                var predictionSet = JsonSerializer.Deserialize<PredictionSet>(
                    json,
                    JsonDataOptions.SerializerOptions);

                if (predictionSet is not null)
                {
                    predictions.Add(predictionSet);
                }
            }
            catch (JsonException)
            {
                // Ignore a corrupt legacy draft and continue synchronizing the remaining entries.
            }
        }

        return predictions;
    }

    public async Task DeleteAsync(string competitionId, CancellationToken cancellationToken = default)
    {
        await localStorage.RemoveItemAsync(LocalStorageKeys.Predictions(competitionId), cancellationToken);
    }
}
