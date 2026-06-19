using TotalCall.Admin.Host.Components;
using TotalCall.Admin.Host.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents();
builder.Services.AddSingleton(AdminRuntimeOptions.FromConfiguration(builder.Configuration));
builder.Services.AddSingleton<AdminRuntimeStatusService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>();
app.MapGet("/healthz", (AdminRuntimeStatusService status) => Results.Ok(status.GetStatus()));

app.Run();
