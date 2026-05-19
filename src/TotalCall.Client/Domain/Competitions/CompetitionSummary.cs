namespace TotalCall.Client.Domain.Competitions;

public sealed record CompetitionSummary
{
    public required string Id { get; init; }

    public required string Slug { get; init; }

    public required string Name { get; init; }

    public string? Description { get; init; }

    public DateTimeOffset? StartDate { get; init; }

    public DateTimeOffset? PredictionLockAt { get; init; }

    public string? CardBackgroundImageUrl { get; init; }

    public string? CardBackgroundPosition { get; init; }

    public string? CardLogoImageUrl { get; init; }

    public string? CardLogoAlt { get; init; }

    public CompetitionStatus Status { get; init; } = CompetitionStatus.Upcoming;

    public required string ConfigVersion { get; init; }
}
