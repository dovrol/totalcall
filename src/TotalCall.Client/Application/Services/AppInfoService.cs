using System.Reflection;

namespace TotalCall.Client.Application.Services;

public sealed class AppInfoService
{
    private const string FallbackVersion = "0.1.0-dev";
    private readonly string appVersion;

    public AppInfoService()
    {
        appVersion = ResolveAppVersion();
    }

    public string AppVersion => appVersion;

    public string DisplayVersion => $"v{TrimBuildMetadata(appVersion)}";

    public string FullDisplayVersion => $"v{appVersion}";

    private static string ResolveAppVersion()
    {
        var attribute = typeof(AppInfoService).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>();

        var value = attribute?.InformationalVersion?.Trim();

        if (string.IsNullOrWhiteSpace(value))
        {
            return FallbackVersion;
        }

        return value.StartsWith('v')
            ? value[1..]
            : value;
    }

    private static string TrimBuildMetadata(string version)
    {
        var plusIndex = version.IndexOf('+');

        return plusIndex > 0
            ? version[..plusIndex]
            : version;
    }
}
