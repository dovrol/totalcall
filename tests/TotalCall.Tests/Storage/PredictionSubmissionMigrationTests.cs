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
    public void Migration_KeepsSubmissionsPrivateAndDoesNotCreateADefinerView()
    {
        var sql = ReadMigration();

        Assert.DoesNotContain(
            "grant select on public.prediction_submissions to anon",
            sql,
            StringComparison.OrdinalIgnoreCase);

        // Participants are exposed through a security definer function in the
        // display-names migration, never through a (definer) view.
        Assert.DoesNotContain(
            "create or replace view public.prediction_participants_public",
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
