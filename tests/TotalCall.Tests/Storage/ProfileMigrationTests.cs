namespace TotalCall.Tests.Storage;

public sealed class ProfileMigrationTests
{
    [Fact]
    public void HandleNewAuthUser_UsesMetadataDisplayNameOrFullName()
    {
        var sql = ReadMigration();

        Assert.Contains("private.profile_display_name_from_metadata(new.raw_user_meta_data, new.id)", sql);
        Assert.Contains("p_raw_user_meta_data ->> 'display_name'", sql);
        Assert.Contains("p_raw_user_meta_data ->> 'full_name'", sql);
        Assert.Contains("insert into public.profiles (id, display_name)", sql);
    }

    [Fact]
    public void DefaultProfileDisplayName_UsesSafeNicknameWithoutEmailOrPlainUserId()
    {
        var sql = ReadMigration();
        var functionSql = ExtractFunction(sql, "private.default_profile_display_name");
        var candidateFunctionSql = ExtractFunction(sql, "public.powerlifting_display_name_candidate");

        Assert.Contains("BenchGoblin", candidateFunctionSql);
        Assert.Contains("SquatWizard", candidateFunctionSql);
        Assert.Contains("DeadliftGremlin", candidateFunctionSql);
        Assert.Contains("DepthPolice", candidateFunctionSql);
        Assert.Contains("WhiteLightEnjoyer", candidateFunctionSql);
        Assert.Contains("lpad", candidateFunctionSql);
        Assert.Contains("length(v_candidate) > 32", candidateFunctionSql);
        Assert.Contains("public.powerlifting_display_name_candidate(p_user_id::text, v_attempt)", functionSql);
        Assert.DoesNotContain("Uczestnik", candidateFunctionSql);
        Assert.DoesNotContain("email", functionSql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("email", candidateFunctionSql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ExistingEmptyDisplayNames_AreBackfilled()
    {
        var sql = ReadMigration();

        Assert.Contains("update public.profiles", sql);
        Assert.Contains("set display_name = private.default_profile_display_name(id)", sql);
        Assert.Contains("display_name is null", sql);
        Assert.Contains("length(btrim(display_name)) = 0", sql);
    }

    [Fact]
    public void DisplayNameValidation_TrimsAndRejectsTooLongHtmlControlCharactersAndUnsupportedPunctuation()
    {
        var sql = ReadMigration();

        Assert.Contains("private.validate_profile_display_name", sql);
        Assert.Contains("btrim(coalesce(p_display_name, ''))", sql);
        Assert.Contains("length(v_display_name) > 32", sql);
        Assert.Contains("v_display_name !~ '^[A-Za-z0-9 ._-]+$'", sql);
        Assert.Contains("profiles_display_name_valid", sql);
        Assert.Contains("display_name = btrim(display_name)", sql);
        Assert.Contains("length(display_name) between 1 and 32", sql);
        Assert.Contains("display_name ~ '^[A-Za-z0-9 ._-]+$'", sql);
    }

    [Fact]
    public void UniqueConstraint_BlocksCaseInsensitiveDuplicateDisplayNames()
    {
        var sql = ReadMigration();

        Assert.Contains("create unique index profiles_display_name_ci_unique_idx", sql);
        Assert.Contains("on public.profiles (lower(display_name))", sql);
        Assert.Contains("where length(btrim(display_name)) > 0", sql);
    }

    [Fact]
    public void Generator_TriesNextVariantWhenDisplayNameIsTaken()
    {
        var sql = ReadMigration();
        var functionSql = ExtractFunction(sql, "private.default_profile_display_name");

        Assert.Contains("for v_attempt in 0..127 loop", functionSql);
        Assert.Contains("not exists", functionSql);
        Assert.Contains("lower(display_name) = lower(v_candidate)", functionSql);
        Assert.Contains("id <> p_user_id", functionSql);
        Assert.Contains("p_user_id::text || ':fallback'", functionSql);

        Assert.Contains("with duplicate_profiles as", sql);
        Assert.Contains("duplicate_position > 1", sql);
    }

    [Fact]
    public void ProfilesRemainOwnerOnlyWithoutPublicSelect()
    {
        var sql = ReadMigration();

        Assert.Contains("revoke all on public.profiles from public, anon", sql);
        Assert.Contains("grant select, insert, update on public.profiles to authenticated", sql);
        Assert.DoesNotContain(
            "grant select on public.profiles to anon",
            sql,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ParticipantsView_StillDoesNotExposePrivateProfileOrSubmissionData()
    {
        var sql = ReadMigration();
        var viewStart = sql.IndexOf(
            "create or replace view public.prediction_participants_public",
            StringComparison.OrdinalIgnoreCase);
        var commentStart = sql.IndexOf("comment on view public.prediction_participants_public", StringComparison.OrdinalIgnoreCase);
        var viewSql = sql[viewStart..commentStart];
        var fromStart = viewSql.IndexOf("from public.prediction_submissions", StringComparison.OrdinalIgnoreCase);
        var selectedColumnsSql = viewSql[..fromStart];

        Assert.Contains("p.display_name", viewSql);
        Assert.Contains("public.powerlifting_display_name_candidate(ps.user_id::text, 0)", viewSql);
        Assert.Contains("where ps.status = 'submitted'", viewSql);
        Assert.DoesNotContain("answers_json", viewSql);
        Assert.DoesNotContain("email", viewSql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(" as user_id", selectedColumnsSql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ps.user_id as", selectedColumnsSql, StringComparison.OrdinalIgnoreCase);
    }

    private static string ExtractFunction(string sql, string functionName)
    {
        var functionStart = sql.IndexOf(
            $"create or replace function {functionName}",
            StringComparison.OrdinalIgnoreCase);
        var functionEnd = sql.IndexOf("$$;", functionStart, StringComparison.OrdinalIgnoreCase);

        return sql[functionStart..(functionEnd + 3)];
    }

    private static string ReadMigration(string fileName = "20260605170000_add_profile_display_names.sql")
    {
        var path = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "../../../../../supabase/migrations",
            fileName));

        return File.ReadAllText(path);
    }
}
