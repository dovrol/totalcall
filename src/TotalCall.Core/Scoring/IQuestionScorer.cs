using TotalCall.Core.Domain.Predictions;

namespace TotalCall.Core.Scoring;

public interface IQuestionScorer
{
    PredictionQuestionType QuestionType { get; }

    QuestionScoreResult Score(QuestionScoringContext context);
}
