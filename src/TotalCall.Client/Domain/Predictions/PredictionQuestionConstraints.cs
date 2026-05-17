namespace TotalCall.Client.Domain.Predictions;

public sealed record PredictionQuestionConstraints
{
    public int? MinSelections { get; init; }

    public int? MaxSelections { get; init; }

    public int? ExactSelections { get; init; }

    public decimal? MinValue { get; init; }

    public decimal? MaxValue { get; init; }

    public decimal? Step { get; init; }

    public string? Unit { get; init; }

    public bool DisallowDuplicateAthletes { get; init; } = true;
}
