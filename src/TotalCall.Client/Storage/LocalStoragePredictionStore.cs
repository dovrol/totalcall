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

    public async Task DeleteAsync(string competitionId, CancellationToken cancellationToken = default)
    {
        await localStorage.RemoveItemAsync(LocalStorageKeys.Predictions(competitionId), cancellationToken);
    }
}
