namespace TotalCall.Tests.Storage;

public sealed class PredictionSubmissionMigrationTests
{
    [Fact]
    public void SubmitPredictionFunction_UsesServerSideTimestampAndPreservesExistingSubmittedAt()
    {
        var sql = ReadMigration();

        Assert.Contains("public.submit_prediction", sql);
        Assert.Contains("status = 'submitted'", sql);
        Assert.Contains("now()", sql);
        Assert.Contains("coalesce(\n      public.prediction_submissions.submitted_at", sql);
        Assert.DoesNotContain("p_submitted_at", sql);
        Assert.Contains("public.submit_prediction(text, jsonb, text, integer)", sql);
    }

    [Fact]
    public void PublicParticipantsView_ExposesOnlySafeSubmittedRows()
    {
        var sql = ReadMigration();
        var viewStart = sql.IndexOf(
            "create or replace view public.prediction_participants_public",
            StringComparison.OrdinalIgnoreCase);
        var commentStart = sql.IndexOf("comment on view public.prediction_participants_public", StringComparison.OrdinalIgnoreCase);
        var viewSql = sql[viewStart..commentStart];
        var fromStart = viewSql.IndexOf("from public.prediction_submissions", StringComparison.OrdinalIgnoreCase);
        var selectedColumnsSql = viewSql[..fromStart];

        Assert.DoesNotContain("public_submission_id", viewSql);
        Assert.Contains("ps.competition_id", viewSql);
        Assert.Contains("display_name", viewSql);
        Assert.Contains("'ChalkyBenchGoblin'", viewSql);
        Assert.Contains("md5(ps.id::text)", viewSql);
        Assert.Contains("ps.submitted_at", viewSql);
        Assert.Contains("ps.status::text as status", viewSql);
        Assert.Contains("where ps.status = 'submitted'", viewSql);

        Assert.DoesNotContain("answers_json", viewSql);
        Assert.DoesNotContain("email", viewSql);
        Assert.DoesNotContain("user_id", selectedColumnsSql);
    }

    [Fact]
    public void Migration_DoesNotGrantPublicSelectOnPredictionSubmissions()
    {
        var sql = ReadMigration();

        Assert.DoesNotContain(
            "grant select on public.prediction_submissions to anon",
            sql,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "grant select on public.prediction_participants_public to anon, authenticated",
            sql,
            StringComparison.OrdinalIgnoreCase);
    }

    private static string ReadMigration()
    {
        var path = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "../../../../../supabase/migrations/20260605150000_add_prediction_submit_and_participants.sql"));

        return File.ReadAllText(path);
    }
}
