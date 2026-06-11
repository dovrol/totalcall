using TotalCall.Client.Domain.Athletes;
using TotalCall.Client.Domain.Predictions;

namespace TotalCall.Client.Domain.Competitions;

public sealed record Competition
{
    public required string Id { get; init; }

    public required string Slug { get; init; }

    public required string Name { get; init; }

    public string? Description { get; init; }

    public string? Federation { get; init; }

    public CompetitionDiscipline? Discipline { get; init; }

    public DateTimeOffset? StartDate { get; init; }

    public DateTimeOffset? EndDate { get; init; }

    public DateTimeOffset? PredictionOpenAt { get; init; }

    public DateTimeOffset? PredictionLockAt { get; init; }

    public string? CardBackgroundImageUrl { get; init; }

    public string? CardBackgroundPosition { get; init; }

    public string? CardLogoImageUrl { get; init; }

    public string? CardLogoAlt { get; init; }

    public CompetitionStatus Status { get; init; } = CompetitionStatus.Upcoming;

    public required string ConfigVersion { get; init; }

    public AthleteDataConfiguration? AthleteData { get; init; }

    public IReadOnlyList<Athlete> Athletes { get; init; } = [];

    public IReadOnlyList<WeightCategory> Categories { get; init; } = [];

    public IReadOnlyList<PredictionGroup> PredictionGroups { get; init; } = [];
}

public sealed record AthleteDataConfiguration
{
    public string? DefaultSource { get; init; }

    public IReadOnlyList<string> AvailableSources { get; init; } = [];
}
