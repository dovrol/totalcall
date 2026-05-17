using TotalCall.Client.Domain.Competitions;
using TotalCall.Client.Domain.Predictions;

namespace TotalCall.Client.Scoring;

public sealed class PredictionScoringService(IEnumerable<IQuestionScorer> questionScorers) : IPredictionScoringService
{
    private readonly IReadOnlyDictionary<PredictionQuestionType, IQuestionScorer> scorers =
        questionScorers.ToDictionary(scorer => scorer.QuestionType);

    public TotalScoreResult Score(Competition competition, PredictionSet predictionSet)
    {
        var questionScores = new List<QuestionScoreResult>();

        foreach (var group in competition.PredictionGroups)
        {
            foreach (var question in group.Questions)
            {
                if (!scorers.TryGetValue(question.Type, out var scorer))
                {
                    continue;
                }

                var answer = predictionSet.Answers.FirstOrDefault(candidate => candidate.QuestionId == question.Id);

                if (answer is null)
                {
                    continue;
                }

                questionScores.Add(scorer.Score(new QuestionScoringContext(competition, group, question, answer)));
            }
        }

        return new TotalScoreResult(questionScores.Sum(score => score.Points), questionScores);
    }
}
