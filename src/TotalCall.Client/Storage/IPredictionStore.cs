using TotalCall.Client.Domain.Predictions;

namespace TotalCall.Client.Storage;

public interface IPredictionStore
{
    Task<PredictionSet?> GetAsync(string competitionId, CancellationToken cancellationToken = default);

    Task SaveAsync(PredictionSet predictionSet, CancellationToken cancellationToken = default);

    Task<PredictionSet> SubmitAsync(PredictionSet predictionSet, CancellationToken cancellationToken = default);

    Task DeleteAsync(string competitionId, CancellationToken cancellationToken = default);
}
