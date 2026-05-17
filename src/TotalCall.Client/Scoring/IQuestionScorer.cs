using TotalCall.Client.Domain.Predictions;

namespace TotalCall.Client.Scoring;

public interface IQuestionScorer
{
    PredictionQuestionType QuestionType { get; }

    QuestionScoreResult Score(QuestionScoringContext context);
}
