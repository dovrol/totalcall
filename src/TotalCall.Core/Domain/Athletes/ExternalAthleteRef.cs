namespace TotalCall.Core.Domain.Athletes;

public sealed record ExternalAthleteRef
{
    public required string Source { get; init; }

    public required string Name { get; init; }

    public string? ExternalId { get; init; }
}

public static class ExternalAthleteSources
{
    public const string OpenIpf = "openipf";

    public const string OpenPowerlifting = "openpowerlifting";
}
