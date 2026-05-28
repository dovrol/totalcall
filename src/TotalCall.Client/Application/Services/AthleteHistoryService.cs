using System.Net.Http.Json;
using System.Text.Json;
using TotalCall.Client.Domain.Athletes;

namespace TotalCall.Client.Application.Services;

public sealed class AthleteHistoryService(HttpClient? httpClient)
{
    private const string DefaultImportStatusSource = ExternalAthleteSources.OpenIpf;

    private static readonly JsonSerializerOptions SupabaseJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true
    };

    private readonly Dictionary<string, AthleteHistoryEntry?> _cache = new(StringComparer.OrdinalIgnoreCase);
    private Task<AthleteDataImportStatus?>? _importStatusTask;

    /// <summary>
    /// Fetch athlete competition history from Supabase by slug.
    /// Returns null when the athlete has no results.
    /// Throws <see cref="InvalidOperationException"/> when Supabase is not configured.
    /// Throws <see cref="HttpRequestException"/> on network/API errors.
    /// </summary>
    public async Task<AthleteHistoryEntry?> GetAthleteHistoryAsync(
        string athleteSlug,
        CancellationToken cancellationToken = default)
    {
        if (httpClient is null)
        {
            throw new InvalidOperationException(
                "Supabase is not configured. Set Supabase:Url and Supabase:PublishableKey in wwwroot/appsettings.json.");
        }

        if (_cache.TryGetValue(athleteSlug, out var cached))
        {
            return cached;
        }

        var url = "rest/v1/athlete_history_view"
                  + $"?athlete_slug=eq.{Uri.EscapeDataString(athleteSlug)}"
                  + "&order=meet_date.desc"
                  + "&select=athlete_display_name,athlete_country_code,"
                  + "meet_date,meet_name,federation,equipment,bodyweight_kg,"
                  + "best_squat_kg,best_bench_kg,best_deadlift_kg,total_kg,place";

        var rows = await httpClient.GetFromJsonAsync<List<HistoryRow>>(
            url, SupabaseJsonOptions, cancellationToken);

        var entry = rows is null || rows.Count == 0
            ? null
            : MapToEntry(rows);

        _cache[athleteSlug] = entry;
        return entry;
    }

    /// <summary>
    /// Fetch public import status metadata for the athlete-history source.
    /// Returns null when Supabase is unavailable or the RPC cannot be read.
    /// </summary>
    public Task<AthleteDataImportStatus?> GetImportStatusAsync(
        CancellationToken cancellationToken = default)
    {
        if (httpClient is null)
        {
            return Task.FromResult<AthleteDataImportStatus?>(null);
        }

        return _importStatusTask ??= FetchImportStatusAsync(cancellationToken);
    }

    private async Task<AthleteDataImportStatus?> FetchImportStatusAsync(CancellationToken cancellationToken)
    {
        if (httpClient is null)
        {
            return null;
        }

        try
        {
            var url = "rest/v1/rpc/get_athlete_data_import_status"
                      + $"?p_source={Uri.EscapeDataString(DefaultImportStatusSource)}";

            var rows = await httpClient.GetFromJsonAsync<List<ImportStatusRow>>(
                url, SupabaseJsonOptions, cancellationToken);

            var row = rows?.FirstOrDefault();
            if (row is null || string.IsNullOrWhiteSpace(row.SourceLabel))
            {
                return null;
            }

            return new AthleteDataImportStatus
            {
                Source = row.Source,
                SourceLabel = row.SourceLabel,
                LastSuccessfulImportAt = row.LastSuccessfulImportAt
            };
        }
        catch
        {
            return null;
        }
    }

    private static AthleteHistoryEntry MapToEntry(List<HistoryRow> rows)
    {
        var recentResults = rows
            .Select(row => new AthleteRecentResult
            {
                Date = row.MeetDate,
                MeetName = row.MeetName,
                Federation = row.Federation,
                Equipment = row.Equipment,
                BodyweightKg = row.BodyweightKg,
                SquatKg = row.BestSquatKg,
                BenchKg = row.BestBenchKg,
                DeadliftKg = row.BestDeadliftKg,
                TotalKg = row.TotalKg,
                Placing = row.Place
            })
            .ToList();

        var first = rows[0];

        return new AthleteHistoryEntry
        {
            DisplayName = first.AthleteDisplayName,
            CountryCode = first.AthleteCountryCode,
            RecentResults = recentResults,
            Bests = new AthleteLiftBests
            {
                SquatKg = MaxPositive(recentResults, r => r.SquatKg),
                BenchKg = MaxPositive(recentResults, r => r.BenchKg),
                DeadliftKg = MaxPositive(recentResults, r => r.DeadliftKg),
                TotalKg = MaxPositive(recentResults, r => r.TotalKg)
            },
            LastResult = new AthleteLastResult
            {
                Date = first.MeetDate,
                MeetName = first.MeetName,
                SquatKg = first.BestSquatKg,
                BenchKg = first.BestBenchKg,
                DeadliftKg = first.BestDeadliftKg,
                TotalKg = first.TotalKg,
                BodyweightKg = first.BodyweightKg
            }
        };
    }

    private static decimal? MaxPositive(
        IEnumerable<AthleteRecentResult> results,
        Func<AthleteRecentResult, decimal?> selector)
    {
        var values = results
            .Select(selector)
            .Where(v => v is > 0)
            .ToList();

        return values.Count == 0 ? null : values.Max();
    }

    /// <summary>PostgREST row from athlete_history_view.</summary>
    private sealed record HistoryRow
    {
        public string? AthleteDisplayName { get; init; }
        public string? AthleteCountryCode { get; init; }
        public string? MeetDate { get; init; }
        public string? MeetName { get; init; }
        public string? Federation { get; init; }
        public string? Equipment { get; init; }
        public decimal? BodyweightKg { get; init; }
        public decimal? BestSquatKg { get; init; }
        public decimal? BestBenchKg { get; init; }
        public decimal? BestDeadliftKg { get; init; }
        public decimal? TotalKg { get; init; }
        public string? Place { get; init; }
    }

    /// <summary>PostgREST row from get_athlete_data_import_status().</summary>
    private sealed record ImportStatusRow
    {
        public string? Source { get; init; }
        public string? SourceLabel { get; init; }
        public DateTimeOffset? LastSuccessfulImportAt { get; init; }
    }
}
