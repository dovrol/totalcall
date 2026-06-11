namespace TotalCall.Tests.Storage;

public sealed class CompetitionDefinitionsMigrationTests
{
    [Fact]
    public void HardenedTrigger_FailsClosedForUnknownOrUnpublishedCompetitions()
    {
        var sql = ReadMigration("20260611120000_harden_competition_definitions.sql");

        Assert.Contains("create or replace function public.enforce_prediction_window()", sql);
        Assert.Contains("if not found then", sql);
        Assert.Contains("Competition % is not configured.", sql);
        Assert.Contains("v_competition.published_version_id is null", sql);
        Assert.Contains("Competition % has no published config version.", sql);
        Assert.Contains("new.competition_version_id := v_competition.published_version_id", sql);
        Assert.Contains("using errcode = '23503'", sql);
    }

    [Fact]
    public void CompetitionVersions_AreImmutableOnceCreated()
    {
        var sql = ReadMigration("20260611120000_harden_competition_definitions.sql");

        Assert.Contains("create or replace function public.enforce_competition_version_immutability()", sql);
        Assert.Contains("old.competition_id is distinct from new.competition_id", sql);
        Assert.Contains("old.version is distinct from new.version", sql);
        Assert.Contains("old.config is distinct from new.config", sql);
        Assert.Contains("Create a new configVersion instead.", sql);
        Assert.Contains("before update of competition_id, version, config on public.competition_versions", sql);
    }

    [Fact]
    public void PublicAccess_DoesNotGrantDirectConfigReadsForAllVersions()
    {
        var sql = ReadMigration("20260611120000_harden_competition_definitions.sql");

        Assert.Contains("drop policy if exists \"competition versions public read\"", sql);
        Assert.Contains("competition versions published metadata read", sql);
        Assert.Contains("where c.published_version_id = competition_versions.id", sql);
        Assert.Contains("revoke all on public.competition_versions from public, anon, authenticated", sql);
        Assert.Contains("grant select (id, competition_id, version, published_at)", sql);
        Assert.DoesNotContain(
            "grant select on public.competition_versions to anon",
            sql,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            "grant select on public.competition_versions to authenticated",
            sql,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PublishedRuntimeSurface_StillExposesOnlyPublishedListAndRpc()
    {
        var baseSql = ReadMigration("20260609120000_add_competition_definitions.sql");
        var hardeningSql = ReadMigration("20260611120000_harden_competition_definitions.sql");

        Assert.Contains("create view public.published_competitions", baseSql);
        Assert.Contains("create or replace function public.get_published_competition(p_slug text)", baseSql);
        Assert.Contains("grant execute on function public.get_published_competition(text) to anon, authenticated", baseSql);
        Assert.Contains("create or replace view public.published_competitions", hardeningSql);
        Assert.Contains("join public.competition_versions v", hardeningSql);
        Assert.DoesNotContain("left join public.competition_versions v", hardeningSql);
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
