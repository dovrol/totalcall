namespace TotalCall.Tests.Storage;

public sealed class PublicBoardMigrationTests
{
    [Fact]
    public void Leaderboard_Rpc_exposes_opaque_board_ref_without_private_submission_data()
    {
        var sql = ReadMigration("20260615120000_add_public_boards.sql");
        var functionSql = ExtractFunction(sql, "public.get_competition_leaderboard");
        var signatureSql = functionSql[..functionSql.IndexOf("language sql", StringComparison.OrdinalIgnoreCase)];

        Assert.Contains("security definer", functionSql);
        Assert.Contains("set search_path = public", functionSql);
        Assert.Contains("board_ref           uuid", signatureSql);
        Assert.Contains("snapshot_id as board_ref", functionSql);
        Assert.Contains("from public.score_snapshots ss", functionSql);
        Assert.Contains("ss.scored_groups_count > 0", functionSql);
        Assert.Contains("grant execute on function public.get_competition_leaderboard(text) to anon, authenticated", sql);

        Assert.DoesNotContain("prediction_submissions", functionSql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("answers_json", functionSql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("email", functionSql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(" user_id ", signatureSql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PublicBoard_Rpc_returns_only_submitted_picks_after_lock_and_scoring()
    {
        var sql = ReadMigration("20260615120000_add_public_boards.sql");
        var functionSql = ExtractFunction(sql, "public.get_public_board");
        var signatureSql = functionSql[..functionSql.IndexOf("language sql", StringComparison.OrdinalIgnoreCase)];

        Assert.Contains("security definer", functionSql);
        Assert.Contains("set search_path = public", functionSql);
        Assert.Contains("picks_json          jsonb", signatureSql);
        Assert.Contains("breakdown_json      jsonb", signatureSql);
        Assert.Contains("join public.prediction_submissions sub on sub.id = ss.prediction_submission_id", functionSql);
        Assert.Contains("coalesce(sub.answers_json -> 'answers', sub.answers_json -> 'Answers') as picks_json", functionSql);
        Assert.Contains("ss.scored_groups_count > 0", functionSql);
        Assert.Contains("sub.status = 'submitted'", functionSql);
        Assert.Contains("sub.submitted_at is not null", functionSql);
        Assert.Contains("c.prediction_lock_at is not null", functionSql);
        Assert.Contains("c.prediction_lock_at <= now()", functionSql);
        Assert.Contains("grant execute on function public.get_public_board(text, uuid) to anon, authenticated", sql);

        Assert.DoesNotContain("localUserId", functionSql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("savedAt", functionSql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("submissionStatus", functionSql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("email", functionSql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(" user_id ", signatureSql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("answers_json      jsonb", signatureSql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MyScore_Rpc_is_authenticated_and_scoped_to_current_user()
    {
        var sql = ReadMigration("20260614120000_add_get_my_score.sql");
        var functionSql = ExtractFunction(sql, "public.get_my_score");
        var signatureSql = functionSql[..functionSql.IndexOf("language sql", StringComparison.OrdinalIgnoreCase)];

        Assert.Contains("security definer", functionSql);
        Assert.Contains("set search_path = public", functionSql);
        Assert.Contains("from public.score_snapshots ss", functionSql);
        Assert.Contains("ss.user_id = auth.uid()", functionSql);
        Assert.Contains("breakdown_json       jsonb", signatureSql);
        Assert.Contains("revoke all on function public.get_my_score(text) from public, anon, authenticated", sql);
        Assert.Contains("grant execute on function public.get_my_score(text) to authenticated", sql);
        Assert.DoesNotContain(
            "grant execute on function public.get_my_score(text) to anon",
            sql,
            StringComparison.OrdinalIgnoreCase);

        Assert.DoesNotContain("answers_json", functionSql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("email", functionSql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(" user_id ", signatureSql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PredictionSubmissions_Remain_owner_only_and_not_publicly_selectable()
    {
        var sql = ReadMigration("20260604120000_add_cloud_prediction_saves.sql");

        Assert.Contains("alter table public.prediction_submissions enable row level security", sql);
        Assert.Contains("for select\n  to authenticated\n  using ((select auth.uid()) = user_id)", sql);
        Assert.Contains("revoke all on public.prediction_submissions from public, anon, authenticated", sql);
        Assert.Contains("grant select, insert, update on public.prediction_submissions to authenticated", sql);
        Assert.Contains("grant all on public.prediction_submissions to service_role", sql);
        Assert.DoesNotContain(
            "grant select on public.prediction_submissions to anon",
            sql,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            "for select\n  to anon",
            sql,
            StringComparison.OrdinalIgnoreCase);
    }

    private static string ExtractFunction(string sql, string functionName)
    {
        var functionStart = sql.IndexOf(
            $"create or replace function {functionName}",
            StringComparison.OrdinalIgnoreCase);
        var functionEnd = sql.IndexOf("$$;", functionStart, StringComparison.OrdinalIgnoreCase);

        return sql[functionStart..(functionEnd + 3)];
    }

    private static string ReadMigration(string fileName)
    {
        var path = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "../../../../../supabase/migrations",
            fileName));

        return File.ReadAllText(path);
    }
}
