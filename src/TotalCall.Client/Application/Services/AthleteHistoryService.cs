using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using TotalCall.Client.Domain.Athletes;
using TotalCall.Client.Domain.Competitions;

namespace TotalCall.Client.Application.Services;

public sealed class AthleteHistoryService(HttpClient? httpClient)
{
    private const string DefaultHistorySource = ExternalAthleteSources.OpenIpf;
    private const int MinimumBenchmarkCountedAttempts = 30;

    private static readonly JsonSerializerOptions SupabaseJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true
    };

    private readonly Dictionary<string, AthleteHistoryEntry?> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, AthleteAnalytics?> _analyticsCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, AthleteAttemptBenchmark?> _benchmarkCache = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Task<AthleteDataImportStatus?>> _importStatusTasks =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Fetch athlete competition history from Supabase by slug.
    /// Returns null when the athlete has no results.
    /// Throws <see cref="InvalidOperationException"/> when Supabase is not configured.
    /// Throws <see cref="HttpRequestException"/> on network/API errors.
    /// </summary>
    public Task<AthleteHistoryEntry?> GetAthleteHistoryAsync(
        string athleteSlug,
        CancellationToken cancellationToken = default)
    {
        return GetAthleteHistoryAsync(
            athleteSlug, (DateOnly?)null, DefaultHistorySource, cancellationToken: cancellationToken);
    }

    public Task<AthleteHistoryEntry?> GetAthleteHistoryAsync(
        string athleteSlug,
        Competition? competition,
        CancellationToken cancellationToken = default)
    {
        return GetAthleteHistoryAsync(
            athleteSlug, competition, DefaultHistorySource, cancellationToken: cancellationToken);
    }

    public Task<AthleteHistoryEntry?> GetAthleteHistoryAsync(
        string athleteSlug,
        Competition? competition,
        string source,
        bool includeAnalytics = true,
        CancellationToken cancellationToken = default)
    {
        return GetAthleteHistoryAsync(
            athleteSlug,
            ResolveTotalMetricReferenceDate(competition),
            source,
            includeAnalytics,
            cancellationToken);
    }

    public async Task<AthleteHistoryEntry?> GetAthleteHistoryAsync(
        string athleteSlug,
        DateOnly? totalMetricReferenceDate,
        string source,
        bool includeAnalytics = true,
        CancellationToken cancellationToken = default)
    {
        if (httpClient is null)
        {
            throw new InvalidOperationException(
                "Supabase is not configured. Set Supabase:Url and Supabase:PublishableKey in wwwroot/appsettings.json.");
        }

        var referenceDate = totalMetricReferenceDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var normalizedSource = AthleteDataSourcePreferenceService.NormalizeRequiredSource(source);
        var cacheKey = $"{athleteSlug}|{normalizedSource}|{referenceDate:yyyyMMdd}";

        if (_cache.TryGetValue(cacheKey, out var cached))
        {
            return includeAnalytics
                ? await ComposeWithAnalyticsAsync(cached, athleteSlug, normalizedSource, referenceDate, cancellationToken)
                : cached;
        }

        var url = "rest/v1/athlete_history_view"
                  + $"?athlete_slug=eq.{Uri.EscapeDataString(athleteSlug)}"
                  + $"&source_code=eq.{Uri.EscapeDataString(normalizedSource)}"
                  + "&order=meet_date.desc"
                  + "&select=athlete_display_name,athlete_country_code,source_code,"
                  + "meet_date,meet_name,federation,equipment,event,bodyweight_kg,"
                  + "best_squat_kg,best_bench_kg,best_deadlift_kg,total_kg,"
                  + "dots_points,goodlift_points,place";

        var rows = await httpClient.GetFromJsonAsync<List<HistoryRow>>(
            url, SupabaseJsonOptions, cancellationToken);

        if (rows is null || rows.Count == 0)
        {
            _cache[cacheKey] = null;
            return null;
        }

        // Build the base entry from history rows only. Aggregate analytics is a second
        // request (get_athlete_analytics) and is composed lazily, so callers that only
        // need best/last totals (the TopN sheet fill actions) never trigger that RPC.
        var entry = MapToEntry(rows, null, referenceDate);
        _cache[cacheKey] = entry;

        return includeAnalytics
            ? await ComposeWithAnalyticsAsync(entry, athleteSlug, normalizedSource, referenceDate, cancellationToken)
            : entry;
    }

    private async Task<AthleteHistoryEntry?> ComposeWithAnalyticsAsync(
        AthleteHistoryEntry? entry,
        string athleteSlug,
        string normalizedSource,
        DateOnly referenceDate,
        CancellationToken cancellationToken)
    {
        if (entry is null || entry.Analytics is not null)
        {
            return entry;
        }

        var analytics = await GetAthleteAnalyticsAsync(athleteSlug, normalizedSource, cancellationToken);
        if (analytics is null)
        {
            return entry;
        }

        var fullPowerTotals = entry.RecentResults
            .Where(IsFullPowerSbdTotal)
            .ToArray();

        return entry with
        {
            Analytics = EnrichAnalytics(analytics, entry.RecentResults, fullPowerTotals, referenceDate)
        };
    }

    /// <summary>
    /// Fetch aggregate public analytics for one athlete without loading full result history.
    /// Returns null when Supabase is unavailable or no analytics row exists.
    /// </summary>
    public async Task<AthleteAnalytics?> GetAthleteAnalyticsAsync(
        string athleteSlug,
        string source,
        CancellationToken cancellationToken = default)
    {
        if (httpClient is null)
        {
            throw new InvalidOperationException(
                "Supabase is not configured. Set Supabase:Url and Supabase:PublishableKey in wwwroot/appsettings.json.");
        }

        var normalizedSource = AthleteDataSourcePreferenceService.NormalizeRequiredSource(source);
        var cacheKey = $"{athleteSlug}|{normalizedSource}";

        if (_analyticsCache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        var analytics = await FetchAthleteAnalyticsAsync(athleteSlug, normalizedSource, cancellationToken);
        _analyticsCache[cacheKey] = analytics;
        return analytics;
    }

    public Task<AthleteAnalytics?> GetAthleteAnalyticsAsync(
        string athleteSlug,
        CancellationToken cancellationToken = default)
    {
        return GetAthleteAnalyticsAsync(athleteSlug, DefaultHistorySource, cancellationToken);
    }

    public async Task<AthleteAttemptBenchmark?> GetAttemptBenchmarkAsync(
        Competition competition,
        Athlete athlete,
        CancellationToken cancellationToken = default)
    {
        return await GetAttemptBenchmarkAsync(competition, athlete, DefaultHistorySource, cancellationToken);
    }

    public async Task<AthleteAttemptBenchmark?> GetAttemptBenchmarkAsync(
        Competition competition,
        Athlete athlete,
        string source,
        CancellationToken cancellationToken = default)
    {
        var categoryAthleteIds = GetCategoryAthleteIds(competition, athlete);
        var sexAthleteIds = athlete.Sex == AthleteSex.Unspecified
            ? Array.Empty<string>()
            : competition.Athletes
                .Where(item => item.Sex == athlete.Sex)
                .Select(item => item.Id)
                .ToArray();
        var fieldAthleteIds = competition.Athletes
            .Select(item => item.Id)
            .ToArray();

        return await GetAttemptBenchmarkAsync(
            athlete.Id,
            athlete.Sex,
            categoryAthleteIds,
            sexAthleteIds,
            fieldAthleteIds,
            source,
            cancellationToken);
    }

    public static bool IsFullPowerSbdTotal(AthleteRecentResult result)
    {
        return result.TotalKg is > 0 && IsFullPowerSbdEvent(result.Event);
    }

    public async Task<AthleteAttemptBenchmark?> GetAttemptBenchmarkAsync(
        string athleteSlug,
        AthleteSex athleteSex,
        IReadOnlyCollection<string> categoryAthleteIds,
        IReadOnlyCollection<string> sexAthleteIds,
        IReadOnlyCollection<string> fieldAthleteIds,
        string source,
        CancellationToken cancellationToken = default)
    {
        if (httpClient is null)
        {
            return null;
        }

        var scopes = new[]
        {
            new BenchmarkScopeRequest(AthleteAttemptBenchmarkScope.Category, athleteSex, categoryAthleteIds),
            new BenchmarkScopeRequest(AthleteAttemptBenchmarkScope.Sex, athleteSex, sexAthleteIds),
            new BenchmarkScopeRequest(AthleteAttemptBenchmarkScope.Field, AthleteSex.Unspecified, fieldAthleteIds)
        };

        foreach (var scope in scopes)
        {
            var benchmark = await BuildAttemptBenchmarkAsync(
                scope.Scope,
                scope.Sex,
                scope.AthleteIds,
                source,
                cancellationToken);

            if (benchmark?.OverallAttempts.CountedAttempts >= MinimumBenchmarkCountedAttempts)
            {
                return benchmark;
            }
        }

        return null;
    }

    private static IReadOnlyCollection<string> GetCategoryAthleteIds(
        Competition competition,
        Athlete athlete)
    {
        if (string.IsNullOrWhiteSpace(athlete.WeightCategoryId))
        {
            return [];
        }

        var category = competition.Categories.FirstOrDefault(item =>
            string.Equals(item.Id, athlete.WeightCategoryId, StringComparison.OrdinalIgnoreCase));

        if (category?.AthleteIds.Count > 0)
        {
            return category.AthleteIds;
        }

        return competition.Athletes
            .Where(item => string.Equals(item.WeightCategoryId, athlete.WeightCategoryId, StringComparison.OrdinalIgnoreCase))
            .Select(item => item.Id)
            .ToArray();
    }

    private static DateOnly ResolveTotalMetricReferenceDate(Competition? competition)
    {
        var reference = competition?.PredictionLockAt ??
                        competition?.StartDate ??
                        DateTimeOffset.UtcNow;

        return DateOnly.FromDateTime(reference.UtcDateTime);
    }

    private static bool IsFullPowerSbdEvent(string? eventName)
    {
        if (string.IsNullOrWhiteSpace(eventName))
        {
            return true;
        }

        var normalized = eventName
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .ToUpperInvariant();

        return string.Equals(normalized, "SBD", StringComparison.Ordinal);
    }

    /// <summary>
    /// Fetch public import status metadata for the athlete-history source.
    /// Returns null when Supabase is unavailable or the RPC cannot be read.
    /// </summary>
    public Task<AthleteDataImportStatus?> GetImportStatusAsync(
        string source = DefaultHistorySource,
        CancellationToken cancellationToken = default)
    {
        if (httpClient is null)
        {
            return Task.FromResult<AthleteDataImportStatus?>(null);
        }

        var normalizedSource = AthleteDataSourcePreferenceService.NormalizeRequiredSource(source);

        if (_importStatusTasks.TryGetValue(normalizedSource, out var cached))
        {
            return cached;
        }

        var statusTask = FetchImportStatusAsync(normalizedSource, cancellationToken);
        _importStatusTasks[normalizedSource] = statusTask;
        return statusTask;
    }

    private async Task<AthleteDataImportStatus?> FetchImportStatusAsync(
        string source,
        CancellationToken cancellationToken)
    {
        if (httpClient is null)
        {
            return null;
        }

        try
        {
            var url = "rest/v1/rpc/get_athlete_data_import_status"
                      + $"?p_source={Uri.EscapeDataString(source)}";

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

    private async Task<AthleteAnalytics?> FetchAthleteAnalyticsAsync(
        string athleteSlug,
        string source,
        CancellationToken cancellationToken)
    {
        if (httpClient is null)
        {
            return null;
        }

        try
        {
            var url = "rest/v1/rpc/get_athlete_analytics"
                      + $"?p_athlete_slug={Uri.EscapeDataString(athleteSlug)}"
                      + $"&p_source={Uri.EscapeDataString(source)}";

            var rows = await httpClient.GetFromJsonAsync<List<AnalyticsRow>>(
                url, SupabaseJsonOptions, cancellationToken);

            var row = rows?.FirstOrDefault();
            return row is null ? null : MapToAnalytics(row);
        }
        catch
        {
            return null;
        }
    }

    private static AthleteHistoryEntry MapToEntry(
        List<HistoryRow> rows,
        AthleteAnalytics? analytics,
        DateOnly totalMetricReferenceDate)
    {
        var recentResults = rows
            .Select(row => new AthleteRecentResult
            {
                Date = row.MeetDate,
                MeetName = row.MeetName,
                Federation = row.Federation,
                Equipment = row.Equipment,
                Event = row.Event,
                BodyweightKg = row.BodyweightKg,
                SquatKg = row.BestSquatKg,
                BenchKg = row.BestBenchKg,
                DeadliftKg = row.BestDeadliftKg,
                TotalKg = row.TotalKg,
                DotsPoints = row.DotsPoints,
                GoodliftPoints = row.GoodliftPoints,
                Placing = row.Place
            })
            .ToList();

        var first = rows[0];
        var fullPowerTotalResults = recentResults
            .Where(IsFullPowerSbdTotal)
            .ToArray();
        var latestFullPowerTotal = fullPowerTotalResults.FirstOrDefault();
        var enrichedAnalytics = analytics is null
            ? null
            : EnrichAnalytics(analytics, recentResults, fullPowerTotalResults, totalMetricReferenceDate);

        return new AthleteHistoryEntry
        {
            DisplayName = first.AthleteDisplayName,
            CountryCode = first.AthleteCountryCode,
            SourceCode = first.SourceCode,
            RecentResults = recentResults,
            Bests = new AthleteLiftBests
            {
                SquatKg = MaxPositive(recentResults, r => r.SquatKg),
                BenchKg = MaxPositive(recentResults, r => r.BenchKg),
                DeadliftKg = MaxPositive(recentResults, r => r.DeadliftKg),
                TotalKg = MaxPositive(fullPowerTotalResults, r => r.TotalKg)
            },
            LastResult = latestFullPowerTotal is null
                ? null
                : new AthleteLastResult
            {
                Date = latestFullPowerTotal.Date,
                MeetName = latestFullPowerTotal.MeetName,
                SquatKg = latestFullPowerTotal.SquatKg,
                BenchKg = latestFullPowerTotal.BenchKg,
                DeadliftKg = latestFullPowerTotal.DeadliftKg,
                TotalKg = latestFullPowerTotal.TotalKg,
                BodyweightKg = latestFullPowerTotal.BodyweightKg
            },
            Analytics = enrichedAnalytics
        };
    }

    private static AthleteAnalytics MapToAnalytics(AnalyticsRow row)
    {
        return new AthleteAnalytics
        {
            StartsCount = row.StartsCount,
            BestTotalKg = row.BestTotalKg,
            LastTotalKg = row.LastTotalKg,
            Last3AvgTotalKg = row.Last3AvgTotalKg,
            Last5AvgTotalKg = row.Last5AvgTotalKg,
            TotalTrendKg = row.TotalTrendKg,
            BestSquatKg = row.BestSquatKg,
            BestBenchKg = row.BestBenchKg,
            BestDeadliftKg = row.BestDeadliftKg,
            BestDotsPoints = row.BestDotsPoints,
            BestGoodliftPoints = row.BestGoodliftPoints,
            SquatAttempts = ToAttemptSuccessRate(
                row.SquatSuccessRate,
                row.SquatSuccessfulAttempts,
                row.SquatCountedAttempts),
            BenchAttempts = ToAttemptSuccessRate(
                row.BenchSuccessRate,
                row.BenchSuccessfulAttempts,
                row.BenchCountedAttempts),
            DeadliftAttempts = ToAttemptSuccessRate(
                row.DeadliftSuccessRate,
                row.DeadliftSuccessfulAttempts,
                row.DeadliftCountedAttempts),
            OverallAttempts = ToAttemptSuccessRate(
                row.OverallSuccessRate,
                row.OverallSuccessfulAttempts,
                row.OverallCountedAttempts),
            ThirdAttempts = ToAttemptSuccessRate(
                row.ThirdAttemptSuccessRate,
                row.ThirdAttemptSuccessfulAttempts,
                row.ThirdAttemptCountedAttempts)
        };
    }

    private async Task<AthleteAttemptBenchmark?> BuildAttemptBenchmarkAsync(
        AthleteAttemptBenchmarkScope scope,
        AthleteSex scopeSex,
        IReadOnlyCollection<string> athleteIds,
        string source,
        CancellationToken cancellationToken)
    {
        // The cohort is the whole scope (category/sex/field) including the viewed
        // athlete, so it is identical for every athlete in the scope. That lets the
        // aggregate be computed in one server-side RPC and cached + reused, instead
        // of fetching every athlete's analytics individually and summing on the client.
        var cohort = athleteIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (cohort.Length == 0)
        {
            return null;
        }

        var cacheKey = $"{scope}|{source}|{string.Join(',', cohort)}";
        if (_benchmarkCache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        var row = await FetchAttemptBenchmarkRowAsync(cohort, source, cancellationToken);
        var benchmark = row is null
            ? null
            : new AthleteAttemptBenchmark
            {
                Scope = scope,
                Sex = scopeSex,
                ComparedAthleteCount = row.AthleteCount,
                SquatAttempts = ToAttemptSuccessRate(
                    row.SquatSuccessRate, row.SquatSuccessfulAttempts, row.SquatCountedAttempts),
                BenchAttempts = ToAttemptSuccessRate(
                    row.BenchSuccessRate, row.BenchSuccessfulAttempts, row.BenchCountedAttempts),
                DeadliftAttempts = ToAttemptSuccessRate(
                    row.DeadliftSuccessRate, row.DeadliftSuccessfulAttempts, row.DeadliftCountedAttempts),
                OverallAttempts = ToAttemptSuccessRate(
                    row.OverallSuccessRate, row.OverallSuccessfulAttempts, row.OverallCountedAttempts),
                ThirdAttempts = ToAttemptSuccessRate(
                    row.ThirdAttemptSuccessRate, row.ThirdAttemptSuccessfulAttempts, row.ThirdAttemptCountedAttempts)
            };

        _benchmarkCache[cacheKey] = benchmark;
        return benchmark;
    }

    private async Task<BenchmarkRow?> FetchAttemptBenchmarkRowAsync(
        IReadOnlyCollection<string> athleteSlugs,
        string source,
        CancellationToken cancellationToken)
    {
        if (httpClient is null)
        {
            return null;
        }

        try
        {
            var response = await httpClient.PostAsJsonAsync(
                "rest/v1/rpc/get_attempt_benchmark",
                new AttemptBenchmarkRequest { AthleteSlugs = athleteSlugs, Source = source },
                SupabaseJsonOptions,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var rows = await response.Content.ReadFromJsonAsync<List<BenchmarkRow>>(
                SupabaseJsonOptions, cancellationToken);
            return rows?.FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }

    private static AthleteAnalytics EnrichAnalytics(
        AthleteAnalytics analytics,
        IReadOnlyList<AthleteRecentResult> recentResults,
        IReadOnlyList<AthleteRecentResult> fullPowerTotalResults,
        DateOnly totalMetricReferenceDate)
    {
        var datedResults = recentResults
            .Select(result => new
            {
                Result = result,
                Date = TryParseDate(result.Date)
            })
            .Where(item => item.Date is not null)
            .ToArray();

        var years = datedResults
            .Select(item => item.Date!.Value.Year)
            .ToArray();

        var totalValues = fullPowerTotalResults
            .Select(result => result.TotalKg!.Value)
            .ToArray();

        var bestTotal = totalValues.Length == 0 ? (decimal?)null : totalValues.Max();
        var lastTotal = totalValues.Length == 0 ? (decimal?)null : totalValues[0];
        var lastToBestPercent = lastTotal is > 0 && bestTotal is > 0
            ? Math.Round(100m * lastTotal.Value / bestTotal.Value, 1)
            : (decimal?)null;

        var twelveMonthsAgo = totalMetricReferenceDate.AddYears(-1);
        var best12MonthTotal = fullPowerTotalResults
            .Select(result => new
            {
                Result = result,
                Date = TryParseDate(result.Date)
            })
            .Where(item => item.Date is not null &&
                           item.Date >= twelveMonthsAgo &&
                           item.Date <= totalMetricReferenceDate)
            .Select(item => item.Result.TotalKg)
            .Where(total => total is > 0)
            .DefaultIfEmpty()
            .Max();

        var trend = BuildRecentTotalTrend(totalValues);
        var stability = BuildTotalStability(totalValues);

        return analytics with
        {
            StartsCount = recentResults.Count,
            FirstStartYear = years.Length == 0 ? null : years.Min(),
            LastStartYear = years.Length == 0 ? null : years.Max(),
            BestTotalKg = bestTotal,
            LastTotalKg = lastTotal,
            LastTotalToBestPercent = lastToBestPercent,
            Best12MonthTotalKg = best12MonthTotal,
            Last3AvgTotalKg = BuildRecentAverage(totalValues, 3),
            Last5AvgTotalKg = BuildRecentAverage(totalValues, 5),
            TotalTrendKg = trend.TrendKg,
            RecentTotalTrendKg = trend.TrendKg,
            RecentTotalTrendStarts = trend.Starts,
            TotalStabilityKg = stability.StabilityKg,
            TotalStabilityStarts = stability.Starts,
            TotalMetricStartsCount = totalValues.Length,
            BestDotsPoints = MaxPositive(fullPowerTotalResults, result => result.DotsPoints),
            BestGoodliftPoints = MaxPositive(fullPowerTotalResults, result => result.GoodliftPoints)
        };
    }

    private static (decimal? TrendKg, int? Starts) BuildRecentTotalTrend(
        IReadOnlyList<decimal> totalValues)
    {
        var totals = totalValues.Take(3).ToArray();

        if (totals.Length < 2)
        {
            return (null, null);
        }

        return (totals[0] - totals[^1], totals.Length);
    }

    private static (decimal? StabilityKg, int Starts) BuildTotalStability(
        IReadOnlyList<decimal> totalValues)
    {
        var totals = totalValues.Take(5).ToArray();

        if (totals.Length < 3)
        {
            return (null, totals.Length);
        }

        var average = totals.Average();
        var meanAbsoluteDeviation = totals
            .Select(total => Math.Abs(total - average))
            .Average();

        return (Math.Round(meanAbsoluteDeviation, 1), totals.Length);
    }

    private static decimal? BuildRecentAverage(
        IReadOnlyList<decimal> totalValues,
        int count)
    {
        if (totalValues.Count < count)
        {
            return null;
        }

        return Math.Round(totalValues.Take(count).Average(), 1);
    }

    private static DateOnly? TryParseDate(string? value)
    {
        return DateOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            ? parsed
            : null;
    }

    private static AthleteAttemptSuccessRate ToAttemptSuccessRate(
        decimal? ratePercent,
        int successfulAttempts,
        int countedAttempts)
    {
        return new AthleteAttemptSuccessRate
        {
            RatePercent = ratePercent,
            SuccessfulAttempts = successfulAttempts,
            CountedAttempts = countedAttempts
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
        public string? SourceCode { get; init; }
        public string? MeetDate { get; init; }
        public string? MeetName { get; init; }
        public string? Federation { get; init; }
        public string? Equipment { get; init; }
        public string? Event { get; init; }
        public decimal? BodyweightKg { get; init; }
        public decimal? BestSquatKg { get; init; }
        public decimal? BestBenchKg { get; init; }
        public decimal? BestDeadliftKg { get; init; }
        public decimal? TotalKg { get; init; }
        public decimal? DotsPoints { get; init; }
        public decimal? GoodliftPoints { get; init; }
        public string? Place { get; init; }
    }

    /// <summary>PostgREST row from get_athlete_analytics().</summary>
    private sealed record AnalyticsRow
    {
        public string? AthleteSlug { get; init; }
        public int StartsCount { get; init; }
        public decimal? BestTotalKg { get; init; }
        public decimal? LastTotalKg { get; init; }

        [JsonPropertyName("last3_avg_total_kg")]
        public decimal? Last3AvgTotalKg { get; init; }

        [JsonPropertyName("last5_avg_total_kg")]
        public decimal? Last5AvgTotalKg { get; init; }

        public decimal? TotalTrendKg { get; init; }
        public decimal? BestSquatKg { get; init; }
        public decimal? BestBenchKg { get; init; }
        public decimal? BestDeadliftKg { get; init; }
        public decimal? BestDotsPoints { get; init; }
        public decimal? BestGoodliftPoints { get; init; }
        public decimal? SquatSuccessRate { get; init; }
        public int SquatSuccessfulAttempts { get; init; }
        public int SquatCountedAttempts { get; init; }
        public decimal? BenchSuccessRate { get; init; }
        public int BenchSuccessfulAttempts { get; init; }
        public int BenchCountedAttempts { get; init; }
        public decimal? DeadliftSuccessRate { get; init; }
        public int DeadliftSuccessfulAttempts { get; init; }
        public int DeadliftCountedAttempts { get; init; }
        public decimal? OverallSuccessRate { get; init; }
        public int OverallSuccessfulAttempts { get; init; }
        public int OverallCountedAttempts { get; init; }
        public decimal? ThirdAttemptSuccessRate { get; init; }
        public int ThirdAttemptSuccessfulAttempts { get; init; }
        public int ThirdAttemptCountedAttempts { get; init; }
    }

    /// <summary>PostgREST row from get_athlete_data_import_status().</summary>
    private sealed record ImportStatusRow
    {
        public string? Source { get; init; }
        public string? SourceLabel { get; init; }
        public DateTimeOffset? LastSuccessfulImportAt { get; init; }
    }

    private sealed record BenchmarkScopeRequest(
        AthleteAttemptBenchmarkScope Scope,
        AthleteSex Sex,
        IReadOnlyCollection<string> AthleteIds);

    /// <summary>Request body for the get_attempt_benchmark RPC (POST).</summary>
    private sealed record AttemptBenchmarkRequest
    {
        [JsonPropertyName("p_athlete_slugs")]
        public required IReadOnlyCollection<string> AthleteSlugs { get; init; }

        [JsonPropertyName("p_source")]
        public required string Source { get; init; }
    }

    /// <summary>PostgREST row from get_attempt_benchmark().</summary>
    private sealed record BenchmarkRow
    {
        public int AthleteCount { get; init; }
        public decimal? SquatSuccessRate { get; init; }
        public int SquatSuccessfulAttempts { get; init; }
        public int SquatCountedAttempts { get; init; }
        public decimal? BenchSuccessRate { get; init; }
        public int BenchSuccessfulAttempts { get; init; }
        public int BenchCountedAttempts { get; init; }
        public decimal? DeadliftSuccessRate { get; init; }
        public int DeadliftSuccessfulAttempts { get; init; }
        public int DeadliftCountedAttempts { get; init; }
        public decimal? OverallSuccessRate { get; init; }
        public int OverallSuccessfulAttempts { get; init; }
        public int OverallCountedAttempts { get; init; }
        public decimal? ThirdAttemptSuccessRate { get; init; }
        public int ThirdAttemptSuccessfulAttempts { get; init; }
        public int ThirdAttemptCountedAttempts { get; init; }
    }
}
