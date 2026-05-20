using TotalCall.Client.Domain.Competitions;
using TotalCall.Client.Domain.Predictions;

namespace TotalCall.Client.Application.Services;

public interface IPredictionValidationService
{
    PredictionValidationResult Validate(Competition competition, PredictionSet predictionSet);

    PredictionModuleValidationResult ValidateModule(
        Competition competition,
        PredictionGroup group,
        PredictionSet predictionSet);
}
