using Microsoft.Extensions.Hosting;

namespace TotalCall.Admin.Host.Services;

public sealed record AdminRuntimeStatus(
    string EnvironmentName,
    bool HasSupabaseUrl,
    bool HasServiceRoleKey,
    bool IsConfigured,
    string SupabaseOrigin);

public sealed class AdminRuntimeStatusService(AdminRuntimeOptions options, IHostEnvironment environment)
{
    public AdminRuntimeStatus GetStatus() => new(
        environment.EnvironmentName,
        options.HasSupabaseUrl,
        options.HasServiceRoleKey,
        options.IsConfigured,
        options.SupabaseOrigin);
}
