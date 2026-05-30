using System.Text.Json.Serialization;

namespace TotalCall.OplImporter;

// ---- Competition JSON DTOs (subset — only fields the importer needs) ----

public sealed record CompetitionDefinition
{
    [JsonPropertyName("id")] public string Id { get; init; } = "";
    [JsonPropertyName("slug")] public string Slug { get; init; } = "";
    [JsonPropertyName("name")] public string Name { get; init; } = "";
    [JsonPropertyName("federation")] public string? Federation { get; init; }
    [JsonPropertyName("athletes")] public List<CompetitionAthlete> Athletes { get; init; } = [];
}

public sealed record CompetitionAthlete
{
    [JsonPropertyName("id")] public string Id { get; init; } = "";
    [JsonPropertyName("displayName")] public string DisplayName { get; init; } = "";
    [JsonPropertyName("sex")] public string? Sex { get; init; }
    [JsonPropertyName("countryCode")] public string? CountryCode { get; init; }
    [JsonPropertyName("countryName")] public string? CountryName { get; init; }
    [JsonPropertyName("externalAthleteRefs")] public List<ExternalAthleteRefDto> ExternalAthleteRefs { get; init; } = [];
}

public sealed record ExternalAthleteRefDto
{
    [JsonPropertyName("source")] public string Source { get; init; } = "";
    [JsonPropertyName("name")] public string Name { get; init; } = "";
    [JsonPropertyName("externalId")] public string? ExternalId { get; init; }
}

// ---- Internal pipeline records ----

// A single parsed OPL CSV row in our domain shape.
public sealed record OplRow
{
    public string Name { get; init; } = "";
    public string? Sex { get; init; }
    public string? Country { get; init; }
    public string? Event { get; init; }
    public string? Equipment { get; init; }
    public string? Division { get; init; }
    public bool? Tested { get; init; }
    public decimal? Age { get; init; }
    public string? AgeClass { get; init; }
    public string? BirthYearClass { get; init; }
    public decimal? BodyweightKg { get; init; }
    public string? WeightClassKg { get; init; }

    public decimal? Squat1Kg { get; init; }
    public decimal? Squat2Kg { get; init; }
    public decimal? Squat3Kg { get; init; }
    public decimal? Squat4Kg { get; init; }
    public decimal? BestSquatKg { get; init; }

    public decimal? Bench1Kg { get; init; }
    public decimal? Bench2Kg { get; init; }
    public decimal? Bench3Kg { get; init; }
    public decimal? Bench4Kg { get; init; }
    public decimal? BestBenchKg { get; init; }

    public decimal? Deadlift1Kg { get; init; }
    public decimal? Deadlift2Kg { get; init; }
    public decimal? Deadlift3Kg { get; init; }
    public decimal? Deadlift4Kg { get; init; }
    public decimal? BestDeadliftKg { get; init; }

    public decimal? TotalKg { get; init; }
    public string? Place { get; init; }

    public decimal? Dots { get; init; }
    public decimal? Wilks { get; init; }
    public decimal? Glossbrenner { get; init; }
    public decimal? Goodlift { get; init; }

    public string? MeetName { get; init; }
    public DateOnly? Date { get; init; }
    public string? Federation { get; init; }
    public string? ParentFederation { get; init; }
    public string? MeetCountry { get; init; }
    public string? MeetState { get; init; }
    public string? MeetTown { get; init; }
}

public sealed record ImportCounters
{
    public int RowsProcessed;
    public int RowsMatched;
    public int RowsInserted;
    public int RowsUpdated;
    public int RowsSkipped;
    public int RowsDeduplicated;
    public int RowsConflictingDuplicates;
    public int RowsFailed;
}
