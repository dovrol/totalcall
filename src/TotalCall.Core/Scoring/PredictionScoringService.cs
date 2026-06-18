using TotalCall.Core.Domain.Competitions;
using TotalCall.Core.Domain.Predictions;

namespace TotalCall.Core.Scoring;

public sealed class PredictionScoringService(IEnumerable<IQuestionScorer> questionScorers) : IPredictionScoringService
{
    private readonly IReadOnlyDictionary<PredictionQuestionType, IQuestionScorer> scorers =
        questionScorers.ToDictionary(scorer => scorer.QuestionType);

    public TotalScoreResult Score(
        Competition competition,
        PredictionSet predictionSet,
        OfficialCompetitionResults officialResults)
    {
        var questionScores = new List<QuestionScoreResult>();
        var totalGroupsCount = 0;
        var scoredGroupsCount = 0;

        foreach (var group in competition.PredictionGroups)
        {
            foreach (var question in group.Questions)
            {
                if (!scorers.TryGetValue(question.Type, out var scorer))
                {
                    continue;
                }

                if (!group.Required || !question.Required)
                {
                    continue;
                }

                totalGroupsCount++;

                var resultGroup = officialResults.FindFinalGroup(group, question);
                if (resultGroup is null)
                {
                    continue;
                }

                scoredGroupsCount++;

                var answer = predictionSet.Answers.FirstOrDefault(candidate =>
                    string.Equals(candidate.GroupId, group.Id, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(candidate.QuestionId, question.Id, StringComparison.OrdinalIgnoreCase));
                if (answer is null)
                {
                    continue;
                }

                questionScores.Add(scorer.Score(
                    new QuestionScoringContext(competition, group, question, answer, resultGroup)));
            }
        }

        var status = totalGroupsCount > 0 && scoredGroupsCount == totalGroupsCount
            ? ScoreCalculationStatus.Final
            : ScoreCalculationStatus.Partial;

        return new TotalScoreResult(
            questionScores.Sum(score => score.Points),
            questionScores,
            scoredGroupsCount,
            totalGroupsCount,
            status);
    }
}
