using TotalCall.Core.Domain.Competitions;
using TotalCall.Core.Domain.Predictions;

namespace TotalCall.Core.Scoring;

public sealed record QuestionScoringContext(
    Competition Competition,
    PredictionGroup Group,
    PredictionQuestion Question,
    PredictionAnswer Answer,
    OfficialResultGroup ResultGroup);
