using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using TotalCall.Client.Application.Providers;
using TotalCall.Core.Domain.Competitions;
using TotalCall.Client.Infrastructure.Json;

namespace TotalCall.Client.Infrastructure.Supabase;

// Loads competition definitions from Supabase: the list from the public
// `published_competitions` view and a single competition's published config from
// the `get_published_competition` RPC. Lifecycle fields (status/lock/open) come
// from the authoritative competitions columns and override whatever the config
// snapshot carries, so the UI matches backend enforcement. Public reads only —
// the publishable key grants the `anon` role.
public sealed class SupabaseCompetitionProvider(
    HttpClient? httpClient,
    string publishableKey) : ICompetitionProvider
{
    private static readonly JsonSerializerOptions SupabaseJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public bool IsConfigured => httpClient is not null && !string.IsNullOrWhiteSpace(publishableKey);

    public async Task<IReadOnlyList<CompetitionSummary>> GetCompetitionSummariesAsync(
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            return [];
        }

        using var request = BuildRequest(
            HttpMethod.Get,
            "rest/v1/published_competitions"
            + "?select=id,slug,name,status,start_date,end_date,prediction_open_at,prediction_lock_at,summary,version");

        using var response = await httpClient!.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var rows = await response.Content.ReadFromJsonAsync<List<PublishedCompetitionRow>>(
            SupabaseJsonOptions,
            cancellationToken);

        return rows is null ? [] : rows.Select(ToSummary).ToArray();
    }

    public async Task<Competition?> GetCompetitionAsync(
        string slug,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            return null;
        }

        using var request = BuildRequest(HttpMethod.Post, "rest/v1/rpc/get_published_competition");
        request.Content = JsonContent.Create(
            new GetPublishedCompetitionBody { Slug = slug },
            options: SupabaseJsonOptions);

        using var response = await httpClient!.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var rows = await response.Content.ReadFromJsonAsync<List<PublishedCompetitionConfigRow>>(
            SupabaseJsonOptions,
            cancellationToken);

        var row = rows?.FirstOrDefault();
        if (row?.Config is not { ValueKind: JsonValueKind.Object } config)
        {
            return null;
        }

        var competition = config.Deserialize<Competition>(JsonDataOptions.SerializerOptions);
        if (competition is null)
        {
            return null;
        }

        return competition with
        {
            Status = row.Status,
            PredictionOpenAt = row.PredictionOpenAt ?? competition.PredictionOpenAt,
            PredictionLockAt = row.PredictionLockAt ?? competition.PredictionLockAt,
            ConfigVersion = string.IsNullOrWhiteSpace(row.Version) ? competition.ConfigVersion : row.Version!
        };
    }

    private static CompetitionSummary ToSummary(PublishedCompetitionRow row)
    {
        var summary = row.Summary is { ValueKind: JsonValueKind.Object } element
            ? element.Deserialize<CompetitionSummary>(JsonDataOptions.SerializerOptions)
            : null;

        summary ??= new CompetitionSummary
        {
            Id = row.Id,
            Slug = row.Slug,
            Name = row.Name,
            ConfigVersion = row.Version ?? string.Empty
        };

        // Authoritative lifecycle from the competitions table overrides the snapshot.
        return summary with
        {
            Id = row.Id,
            Slug = row.Slug,
            Name = row.Name,
            Status = row.Status,
            StartDate = row.StartDate ?? summary.StartDate,
            PredictionLockAt = row.PredictionLockAt ?? summary.PredictionLockAt,
            ConfigVersion = string.IsNullOrWhiteSpace(row.Version) ? summary.ConfigVersion : row.Version!
        };
    }

    private HttpRequestMessage BuildRequest(HttpMethod method, string url)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.TryAddWithoutValidation("apikey", publishableKey);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", publishableKey);
        return request;
    }

    private sealed record PublishedCompetitionRow
    {
        public required string Id { get; init; }

        public required string Slug { get; init; }

        public required string Name { get; init; }

        public CompetitionStatus Status { get; init; }

        public DateTimeOffset? StartDate { get; init; }

        public DateTimeOffset? EndDate { get; init; }

        public DateTimeOffset? PredictionOpenAt { get; init; }

        public DateTimeOffset? PredictionLockAt { get; init; }

        public JsonElement? Summary { get; init; }

        public string? Version { get; init; }
    }

    private sealed record PublishedCompetitionConfigRow
    {
        public JsonElement? Config { get; init; }

        public CompetitionStatus Status { get; init; }

        public DateTimeOffset? PredictionOpenAt { get; init; }

        public DateTimeOffset? PredictionLockAt { get; init; }

        public string? Version { get; init; }
    }

    private sealed record GetPublishedCompetitionBody
    {
        [JsonPropertyName("p_slug")]
        public required string Slug { get; init; }
    }
}
