using TotalCall.Client.Components.Predictions.TopN;
using TotalCall.Core.Domain.Predictions;

namespace TotalCall.Tests.Components.Predictions.TopN;

public sealed class TopNCategoryNavigationStateTests
{
    [Fact]
    public void Resolve_OrdersQuestionsAndFindsActiveQuestionCaseInsensitively()
    {
        var state = TopNCategoryNavigationState.Resolve(
            CreateGroup(),
            "WOMEN-57",
            CreateValidation());

        Assert.Equal(["women-52", "women-57", "women-63"], state.OrderedQuestions.Select(question => question.Id));
        Assert.Equal("women-57", state.ActiveQuestion?.Id);
        Assert.Equal(1, state.ActiveQuestionIndex);
        Assert.Equal(PredictionCompletionStatus.InProgress, state.ActiveStatusKind);
        Assert.Equal(1, state.CompletedCategoriesCount);
    }

    [Fact]
    public void Resolve_WhenActiveQuestionIsMissing_FallsBackToFirstOrderedQuestion()
    {
        var state = TopNCategoryNavigationState.Resolve(
            CreateGroup(),
            "missing",
            CreateValidation());

        Assert.Equal("women-52", state.ActiveQuestion?.Id);
        Assert.Equal(0, state.ActiveQuestionIndex);
        Assert.Equal(PredictionCompletionStatus.Complete, state.ActiveStatusKind);
    }

    [Fact]
    public void Resolve_StatusLookupIsCaseInsensitive()
    {
        var state = TopNCategoryNavigationState.Resolve(
            CreateGroup(),
            "women-57",
            CreateValidation());

        Assert.True(state.StatusByQuestionId.TryGetValue("WOMEN-57", out var status));
        Assert.Equal(PredictionCompletionStatus.InProgress, status.Status);
    }

    [Fact]
    public void Resolve_WhenGroupHasNoQuestions_ReturnsEmptyState()
    {
        var state = TopNCategoryNavigationState.Resolve(
            CreateGroup([]),
            activeQuestionId: null,
            CreateValidation([]));

        Assert.Empty(state.OrderedQuestions);
        Assert.Null(state.ActiveQuestion);
        Assert.Null(state.ActiveQuestionStatus);
        Assert.Equal(0, state.ActiveQuestionIndex);
        Assert.Equal(0, state.CompletedCategoriesCount);
        Assert.Equal(PredictionCompletionStatus.NotStarted, state.ActiveStatusKind);
    }

    private static PredictionGroup CreateGroup(IReadOnlyList<PredictionQuestion>? questions = null)
    {
        return new PredictionGroup
        {
            Id = "top-n",
            Title = "Top N",
            Questions = questions ??
                        [
                            CreateQuestion("women-63", 3),
                            CreateQuestion("women-52", 1),
                            CreateQuestion("women-57", 2)
                        ]
        };
    }

    private static PredictionQuestion CreateQuestion(string id, int order)
    {
        return new PredictionQuestion
        {
            Id = id,
            Type = PredictionQuestionType.CategoryPodium,
            Title = id,
            Order = order
        };
    }

    private static PredictionModuleValidationResult CreateValidation(
        IReadOnlyList<PredictionQuestionCompletionResult>? questions = null)
    {
        questions ??=
        [
            new PredictionQuestionCompletionResult("women-52", PredictionCompletionStatus.Complete, 3, 3),
            new PredictionQuestionCompletionResult("women-57", PredictionCompletionStatus.InProgress, 2, 3),
            new PredictionQuestionCompletionResult("women-63", PredictionCompletionStatus.NotStarted, 0, 3)
        ];

        return new PredictionModuleValidationResult(
            "top-n",
            PredictionCompletionStatus.InProgress,
            questions.Count,
            questions.Count(question => question.Status == PredictionCompletionStatus.Complete),
            questions.Count(question => question.Status == PredictionCompletionStatus.InProgress),
            questions.Count(question => question.Status == PredictionCompletionStatus.NotStarted),
            questions,
            []);
    }
}
