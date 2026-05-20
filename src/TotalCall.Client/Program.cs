using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using TotalCall.Client;
using TotalCall.Client.Application.Localization;
using TotalCall.Client.Application.Providers;
using TotalCall.Client.Application.Services;
using TotalCall.Client.Application.Theme;
using TotalCall.Client.Infrastructure.Browser;
using TotalCall.Client.Infrastructure.Json;
using TotalCall.Client.Scoring;
using TotalCall.Client.Storage;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
builder.Services.AddScoped<ICompetitionProvider, JsonCompetitionProvider>();
builder.Services.AddScoped<CompetitionService>();
builder.Services.AddScoped<BrowserLocalStorage>();
builder.Services.AddScoped<CultureService>();
builder.Services.AddScoped<ThemeService>();
builder.Services.AddScoped<IPredictionStore, LocalStoragePredictionStore>();
builder.Services.AddScoped<PredictionService>();
builder.Services.AddScoped<IPredictionValidationService, PredictionValidationService>();
builder.Services.AddScoped<PredictionAnswerDisplayService>();
builder.Services.AddScoped<PredictionTextService>();
builder.Services.AddScoped<IPredictionScoringService, PredictionScoringService>();

var host = builder.Build();
await host.Services.GetRequiredService<CultureService>().InitializeAsync();
await host.Services.GetRequiredService<ThemeService>().InitializeAsync();
await host.RunAsync();
