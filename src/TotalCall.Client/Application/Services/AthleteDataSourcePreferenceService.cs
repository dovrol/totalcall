using TotalCall.Core.Domain.Athletes;
using TotalCall.Core.Domain.Competitions;
using TotalCall.Client.Infrastructure.Browser;

namespace TotalCall.Client.Application.Services;

public sealed class AthleteDataSourcePreferenceService(BrowserLocalStorage localStorage)
{
    private readonly Dictionary<string, string> selectedSources = new(StringComparer.OrdinalIgnoreCase);

    public event Action<string, string>? SourceChanged;

    public IReadOnlyList<string> GetAvailableSources(Competition competition)
    {
        var configuredSources = competition.AthleteData?.AvailableSources
            .Select(NormalizeSource)
            .Where(source => source is not null)
            .Select(source => source!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (configuredSources?.Length > 0)
        {
            return configuredSources;
        }

        return [GetConventionDefaultSource(competition)];
    }

    public string GetDefaultSource(Competition competition)
    {
        var availableSources = GetAvailableSources(competition);
        var configuredDefault = NormalizeSource(competition.AthleteData?.DefaultSource);

        return configuredDefault is not null &&
               availableSources.Contains(configuredDefault, StringComparer.OrdinalIgnoreCase)
            ? configuredDefault
            : availableSources[0];
    }

    public async ValueTask<string> GetSourceAsync(
        Competition competition,
        CancellationToken cancellationToken = default)
    {
        if (selectedSources.TryGetValue(competition.Id, out var cached))
        {
            return cached;
        }

        var availableSources = GetAvailableSources(competition);
        var stored = NormalizeSource(await localStorage.GetItemAsync(
            LocalStorageKeys.AthleteDataSource(competition.Id),
            cancellationToken));
        var selected = stored is not null &&
                       availableSources.Contains(stored, StringComparer.OrdinalIgnoreCase)
            ? stored
            : GetDefaultSource(competition);

        selectedSources[competition.Id] = selected;
        return selected;
    }

    public async ValueTask SetSourceAsync(
        Competition competition,
        string source,
        CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeSource(source);
        var availableSources = GetAvailableSources(competition);

        if (normalized is null ||
            !availableSources.Contains(normalized, StringComparer.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                $"Athlete data source '{source}' is not configured for competition '{competition.Id}'.",
                nameof(source));
        }

        if (selectedSources.TryGetValue(competition.Id, out var current) &&
            string.Equals(current, normalized, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        selectedSources[competition.Id] = normalized;
        await localStorage.SetItemAsync(
            LocalStorageKeys.AthleteDataSource(competition.Id),
            normalized,
            cancellationToken);
        SourceChanged?.Invoke(competition.Id, normalized);
    }

    public static string GetSourceLabel(string source)
    {
        return NormalizeSource(source) switch
        {
            ExternalAthleteSources.OpenIpf => "OpenIPF",
            ExternalAthleteSources.OpenPowerlifting => "OpenPowerlifting",
            _ => source
        };
    }

    public static string NormalizeRequiredSource(string source)
    {
        return NormalizeSource(source) ??
               throw new ArgumentException($"Unsupported athlete data source '{source}'.", nameof(source));
    }

    private static string GetConventionDefaultSource(Competition competition)
    {
        return string.Equals(competition.Federation, "IPF", StringComparison.OrdinalIgnoreCase)
            ? ExternalAthleteSources.OpenIpf
            : ExternalAthleteSources.OpenPowerlifting;
    }

    private static string? NormalizeSource(string? source)
    {
        return source?.Trim().ToLowerInvariant() switch
        {
            ExternalAthleteSources.OpenIpf => ExternalAthleteSources.OpenIpf,
            ExternalAthleteSources.OpenPowerlifting => ExternalAthleteSources.OpenPowerlifting,
            _ => null
        };
    }
}
