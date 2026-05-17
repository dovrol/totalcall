namespace TotalCall.Client.Domain.Predictions;

public sealed record PredictionAnswerValue
{
    public bool? BooleanValue { get; init; }

    public decimal? NumericValue { get; init; }

    public string? TextValue { get; init; }

    public string? SelectedOptionId { get; init; }

    public IReadOnlyList<string> SelectedOptionIds { get; init; } = [];

    public string? SelectedAthleteId { get; init; }

    public IReadOnlyList<string> SelectedAthleteIds { get; init; } = [];

    public string? SelectedCategoryId { get; init; }

    public IReadOnlyList<string> SelectedCategoryIds { get; init; } = [];

    public IReadOnlyList<AthletePlacementPick> AthletePlacements { get; init; } = [];
}
