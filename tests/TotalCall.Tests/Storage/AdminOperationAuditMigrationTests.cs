namespace TotalCall.Tests.Storage;

public sealed class AdminOperationAuditMigrationTests
{
    [Fact]
    public void Migration_Adds_private_admin_operation_runs_table()
    {
        var sql = ReadMigration();

        Assert.Contains("create table if not exists public.admin_operation_runs", sql);
        Assert.Contains("operation_type text not null", sql);
        Assert.Contains("status         text not null check", sql);
        Assert.Contains("input_json     jsonb not null default '{}'::jsonb", sql);
        Assert.Contains("result_json    jsonb not null default '{}'::jsonb", sql);
        Assert.Contains("logs_json      jsonb not null default '[]'::jsonb", sql);
    }

    [Fact]
    public void Migration_Keeps_admin_operation_runs_service_role_only()
    {
        var sql = ReadMigration();

        Assert.Contains("alter table public.admin_operation_runs enable row level security", sql);
        Assert.Contains("revoke all on public.admin_operation_runs from public, anon, authenticated", sql);
        Assert.Contains("grant all on public.admin_operation_runs to service_role", sql);
        Assert.DoesNotContain("grant select on public.admin_operation_runs to anon", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("grant select on public.admin_operation_runs to authenticated", sql, StringComparison.OrdinalIgnoreCase);
    }

    private static string ReadMigration()
    {
        var path = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "../../../../../supabase/migrations/20260619120000_add_admin_operation_runs.sql"));

        return File.ReadAllText(path);
    }
}
