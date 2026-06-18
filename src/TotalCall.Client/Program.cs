using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using TotalCall.Client;
using TotalCall.Client.Application.Auth;
using TotalCall.Client.Application.Localization;
using TotalCall.Client.Application.Providers;
using TotalCall.Client.Application.Services;
using TotalCall.Client.Application.Services.Export;
using TotalCall.Client.Application.Services.Notifications;
using TotalCall.Client.Application.Services.Review;
using TotalCall.Client.Application.Theme;
using TotalCall.Client.Infrastructure.Browser;
using TotalCall.Client.Infrastructure.Json;
using TotalCall.Client.Infrastructure.Supabase;
using TotalCall.Client.Infrastructure.Supabase.Auth;
using TotalCall.Core.Scoring;
using TotalCall.Client.Storage;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
builder.Services.AddScoped<JsonCompetitionProvider>();
builder.Services.AddScoped(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var url = config[$"{SupabaseSettings.SectionName}:Url"];
    var key = config[$"{SupabaseSettings.SectionName}:PublishableKey"];

    HttpClient? http = null;
    if (!string.IsNullOrWhiteSpace(url) && !string.IsNullOrWhiteSpace(key))
    {
        http = new HttpClient { BaseAddress = new Uri(url.TrimEnd('/') + "/") };
    }

    return new SupabaseCompetitionProvider(http, key ?? string.Empty);
});
builder.Services.AddScoped<ICompetitionProvider>(sp => new CompositeCompetitionProvider(
    sp.GetRequiredService<SupabaseCompetitionProvider>(),
    sp.GetRequiredService<JsonCompetitionProvider>()));
builder.Services.AddScoped<CompetitionService>();
builder.Services.AddScoped<BrowserLocalStorage>();
builder.Services.AddScoped<BrowserFileActions>();
builder.Services.AddScoped<AthleteDataSourcePreferenceService>();
builder.Services.AddScoped<CultureService>();
builder.Services.AddScoped<ThemeService>();
builder.Services.AddSingleton<AppInfoService>();
builder.Services.AddScoped<ChangelogService>();
builder.Services.AddScoped<LocalStoragePredictionStore>();
builder.Services.AddScoped<PredictionSyncState>();
builder.Services.AddScoped<SynchronizedPredictionStore>();
builder.Services.AddScoped<IPredictionStore>(sp => sp.GetRequiredService<SynchronizedPredictionStore>());
builder.Services.AddScoped<PredictionService>();
builder.Services.AddScoped<IPredictionValidationService, PredictionValidationService>();
builder.Services.AddScoped<PredictionAnswerDisplayService>();
builder.Services.AddScoped<PredictionTextService>();
builder.Services.AddScoped<CompetitionReviewBuilder>();
builder.Services.AddScoped<IPredictionModuleExporter, DefaultPredictionModuleExporter>();
builder.Services.AddScoped<PredictionModuleExporterRegistry>();
builder.Services.AddScoped<PredictionExportService>();
builder.Services.AddScoped<ToastService>();
builder.Services.AddScoped(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var url = config[$"{SupabaseSettings.SectionName}:Url"];
    var key = config[$"{SupabaseSettings.SectionName}:PublishableKey"];

    if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(key))
    {
        Console.Error.WriteLine(
            "[TotalCall] Supabase not configured. " +
            "Set Supabase:Url and Supabase:PublishableKey in wwwroot/appsettings.json. " +
            "Athlete history will be unavailable.");
        return new AthleteHistoryService(null);
    }

    var http = new HttpClient { BaseAddress = new Uri(url.TrimEnd('/') + "/") };
    http.DefaultRequestHeaders.Add("apikey", key);
    http.DefaultRequestHeaders.Add("Authorization", $"Bearer {key}");
    return new AthleteHistoryService(http);
});

// Supabase Auth (magic link / PKCE) — publishable key only, never the secret key.
builder.Services.AddScoped(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var url = config[$"{SupabaseSettings.SectionName}:Url"];
    var key = config[$"{SupabaseSettings.SectionName}:PublishableKey"];

    if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(key))
    {
        Console.Error.WriteLine(
            "[TotalCall] Supabase not configured. " +
            "Set Supabase:Url and Supabase:PublishableKey in wwwroot/appsettings.json. " +
            "Sign-in will be unavailable.");
        url = builder.HostEnvironment.BaseAddress;
        key = string.Empty;
    }

    var http = new HttpClient { BaseAddress = new Uri(url.TrimEnd('/') + "/") };
    return new SupabaseAuthClient(http, key);
});
builder.Services.AddScoped<SupabaseSessionStore>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddAuthorizationCore();
builder.Services.AddScoped<AuthenticationStateProvider, TotalCallAuthenticationStateProvider>();
builder.Services.AddScoped(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var url = config[$"{SupabaseSettings.SectionName}:Url"];
    var key = config[$"{SupabaseSettings.SectionName}:PublishableKey"];

    HttpClient? http = null;
    if (!string.IsNullOrWhiteSpace(url) && !string.IsNullOrWhiteSpace(key))
    {
        http = new HttpClient { BaseAddress = new Uri(url.TrimEnd('/') + "/") };
    }

    return new SupabasePredictionStore(
        http,
        key ?? string.Empty,
        sp.GetRequiredService<AuthService>());
});
builder.Services.AddScoped(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var url = config[$"{SupabaseSettings.SectionName}:Url"];
    var key = config[$"{SupabaseSettings.SectionName}:PublishableKey"];

    HttpClient? http = null;
    if (!string.IsNullOrWhiteSpace(url) && !string.IsNullOrWhiteSpace(key))
    {
        http = new HttpClient { BaseAddress = new Uri(url.TrimEnd('/') + "/") };
    }

    return new SupabaseProfileStore(
        http,
        key ?? string.Empty,
        sp.GetRequiredService<AuthService>());
});

builder.Services.AddScoped<WindowManager>();
builder.Services.AddScoped<IQuestionScorer, AthleteRankingQuestionScorer>();
builder.Services.AddScoped<IQuestionScorer, CategoryPodiumQuestionScorer>();
builder.Services.AddScoped<IPredictionScoringService, PredictionScoringService>();

var host = builder.Build();
await host.Services.GetRequiredService<CultureService>().InitializeAsync();
await host.Services.GetRequiredService<ThemeService>().InitializeAsync();
await host.Services.GetRequiredService<AuthService>().InitializeAsync();
await host.Services.GetRequiredService<SynchronizedPredictionStore>().InitializeAsync();
await host.RunAsync();
