using Microsoft.JSInterop;
using TotalCall.Client.Infrastructure.Browser;

namespace TotalCall.Client.Application.Theme;

public sealed class ThemeService(BrowserLocalStorage localStorage, IJSRuntime jsRuntime)
{
    public event Action? ThemeChanged;

    public string CurrentTheme { get; private set; } = "dark";

    public bool IsDarkMode => string.Equals(CurrentTheme, "dark", StringComparison.OrdinalIgnoreCase);

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var storedTheme = await localStorage.GetItemAsync(LocalStorageKeys.ThemePreference, cancellationToken);
        CurrentTheme = NormalizeTheme(storedTheme);

        await ApplyThemeAsync(CurrentTheme, cancellationToken);
    }

    public async Task ToggleThemeAsync(CancellationToken cancellationToken = default)
    {
        var nextTheme = IsDarkMode ? "light" : "dark";
        await SetThemeAsync(nextTheme, cancellationToken);
    }

    public async Task SetThemeAsync(string theme, CancellationToken cancellationToken = default)
    {
        var normalizedTheme = NormalizeTheme(theme);

        if (string.Equals(CurrentTheme, normalizedTheme, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        CurrentTheme = normalizedTheme;
        await localStorage.SetItemAsync(LocalStorageKeys.ThemePreference, normalizedTheme, cancellationToken);
        await ApplyThemeAsync(normalizedTheme, cancellationToken);

        ThemeChanged?.Invoke();
    }

    private ValueTask ApplyThemeAsync(string theme, CancellationToken cancellationToken)
    {
        return jsRuntime.InvokeVoidAsync("totalCallTheme.set", cancellationToken, theme);
    }

    private static string NormalizeTheme(string? theme)
    {
        return string.Equals(theme, "light", StringComparison.OrdinalIgnoreCase)
            ? "light"
            : "dark";
    }
}
