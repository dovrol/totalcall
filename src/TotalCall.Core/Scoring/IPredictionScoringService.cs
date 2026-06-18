using TotalCall.Core.Domain.Competitions;
using TotalCall.Core.Domain.Predictions;

namespace TotalCall.Core.Scoring;

public interface IPredictionScoringService
{
    TotalScoreResult Score(
        Competition competition,
        PredictionSet predictionSet,
        OfficialCompetitionResults officialResults);
}
