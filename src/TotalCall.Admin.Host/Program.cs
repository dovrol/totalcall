using TotalCall.Admin.Host.Components;
using TotalCall.Admin.Host.Services;
using TotalCall.Operations.Competitions;
using TotalCall.Operations.Results;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddSingleton(AdminRuntimeOptions.FromConfiguration(builder.Configuration));
builder.Services.AddSingleton<AdminRuntimeStatusService>();
builder.Services.AddSingleton<CompetitionConfigFileChecker>();
builder.Services.AddSingleton<CompetitionDefinitionImporter>();
builder.Services.AddSingleton<OfficialResultsImporter>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();
app.MapGet("/healthz", (AdminRuntimeStatusService status) => Results.Ok(status.GetStatus()));

app.Run();
