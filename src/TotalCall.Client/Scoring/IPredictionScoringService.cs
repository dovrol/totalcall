using TotalCall.Client.Domain.Competitions;
using TotalCall.Client.Domain.Predictions;

namespace TotalCall.Client.Scoring;

public interface IPredictionScoringService
{
    TotalScoreResult Score(
        Competition competition,
        PredictionSet predictionSet,
        OfficialCompetitionResults officialResults);
}
