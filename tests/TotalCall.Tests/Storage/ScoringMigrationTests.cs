namespace TotalCall.Tests.Storage;

public sealed class ScoringMigrationTests
{
    [Fact]
    public void Migration_Adds_private_results_and_snapshot_tables_with_idempotent_keys()
    {
        var sql = ReadMigration();

        Assert.Contains("create table public.official_results", sql);
        Assert.Contains("create table public.official_result_groups", sql);
        Assert.Contains("create table public.score_snapshots", sql);
        Assert.Contains("unique (competition_id)", sql);
        Assert.Contains("unique (competition_id, group_id, question_id)", sql);
        Assert.Contains("unique (competition_id, user_id)", sql);
        Assert.Contains("unique (prediction_submission_id)", sql);
        Assert.Contains("results_hash", sql);
        Assert.Contains("result_hash", sql);
    }

    [Fact]
    public void Migration_Keeps_private_tables_service_role_only()
    {
        var sql = ReadMigration();

        Assert.Contains("alter table public.official_results enable row level security", sql);
        Assert.Contains("alter table public.official_result_groups enable row level security", sql);
        Assert.Contains("alter table public.score_snapshots enable row level security", sql);
        Assert.Contains("revoke all on public.official_results from public, anon, authenticated", sql);
        Assert.Contains("revoke all on public.official_result_groups from public, anon, authenticated", sql);
        Assert.Contains("revoke all on public.score_snapshots from public, anon, authenticated", sql);
        Assert.Contains("grant all on public.official_results to service_role", sql);
        Assert.Contains("grant all on public.official_result_groups to service_role", sql);
        Assert.Contains("grant all on public.score_snapshots to service_role", sql);
        Assert.DoesNotContain("grant select on public.score_snapshots to anon", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("grant select on public.official_result_groups to anon", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Leaderboard_Rpc_returns_only_safe_snapshot_projection()
    {
        var sql = ReadMigration();
        var functionSql = ExtractFunction(sql, "public.get_competition_leaderboard");
        var signatureSql = functionSql[..functionSql.IndexOf("language sql", StringComparison.OrdinalIgnoreCase)];

        Assert.Contains("security definer", functionSql);
        Assert.Contains("from public.score_snapshots ss", functionSql);
        Assert.Contains("left join public.profiles p on p.id = ss.user_id", functionSql);
        Assert.Contains("where ss.competition_id = p_competition_id", functionSql);
        Assert.Contains("ss.scored_groups_count > 0", functionSql);
        Assert.Contains("total_points desc", functionSql);
        Assert.Contains("lower(display_name) asc", functionSql);
        Assert.Contains("grant execute on function public.get_competition_leaderboard(text) to anon, authenticated", sql);

        Assert.Contains("display_name text", signatureSql);
        Assert.Contains("\"position\" integer", signatureSql);
        Assert.Contains("total_points numeric", signatureSql);
        Assert.Contains("scored_groups_count integer", signatureSql);
        Assert.Contains("total_groups_count integer", signatureSql);
        Assert.Contains("last_calculated_at timestamptz", signatureSql);
        Assert.DoesNotContain("answers_json", functionSql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("email", functionSql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(" user_id ", signatureSql, StringComparison.OrdinalIgnoreCase);
    }

    private static string ExtractFunction(string sql, string functionName)
    {
        var functionStart = sql.IndexOf(
            $"create or replace function {functionName}",
            StringComparison.OrdinalIgnoreCase);
        var functionEnd = sql.IndexOf("$$;", functionStart, StringComparison.OrdinalIgnoreCase);

        return sql[functionStart..(functionEnd + 3)];
    }

    private static string ReadMigration()
    {
        var path = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "../../../../../supabase/migrations/20260611130000_add_scoring_v1_results.sql"));

        return File.ReadAllText(path);
    }
}
