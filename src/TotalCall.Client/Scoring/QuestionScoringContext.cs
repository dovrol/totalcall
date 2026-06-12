using TotalCall.Client.Domain.Competitions;
using TotalCall.Client.Domain.Predictions;

namespace TotalCall.Client.Scoring;

public sealed record QuestionScoringContext(
    Competition Competition,
    PredictionGroup Group,
    PredictionQuestion Question,
    PredictionAnswer Answer,
    OfficialResultGroup ResultGroup);
