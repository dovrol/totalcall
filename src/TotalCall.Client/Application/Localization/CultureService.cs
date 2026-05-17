using System.Globalization;
using TotalCall.Client.Infrastructure.Browser;

namespace TotalCall.Client.Application.Localization;

public sealed class CultureService(BrowserLocalStorage localStorage)
{
    public event Action? CultureChanged;

    public IReadOnlyList<CultureOption> SupportedCultures { get; } =
    [
        new CultureOption("pl-PL", "PL"),
        new CultureOption("en-US", "EN")
    ];

    public string CurrentCultureName => CultureInfo.CurrentUICulture.Name;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var storedCulture = await localStorage.GetItemAsync(LocalStorageKeys.CulturePreference, cancellationToken) ??
            await localStorage.GetItemAsync(LocalStorageKeys.LanguagePreference, cancellationToken);

        SetCurrentCulture(NormalizeCultureName(storedCulture), notify: false);
    }

    public async Task SetCultureAsync(string cultureName, CancellationToken cancellationToken = default)
    {
        var normalizedCultureName = NormalizeCultureName(cultureName);

        if (string.Equals(CurrentCultureName, normalizedCultureName, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        SetCurrentCulture(normalizedCultureName, notify: true);
        await localStorage.SetItemAsync(LocalStorageKeys.CulturePreference, normalizedCultureName, cancellationToken);
    }

    private void SetCurrentCulture(string cultureName, bool notify)
    {
        var culture = CultureInfo.GetCultureInfo(cultureName);

        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;

        if (notify)
        {
            CultureChanged?.Invoke();
        }
    }

    private string NormalizeCultureName(string? cultureName)
    {
        var normalizedCultureName = cultureName?.Trim().ToLowerInvariant();

        return normalizedCultureName switch
        {
            "en" or "en-us" => "en-US",
            _ => "pl-PL"
        };
    }
}
