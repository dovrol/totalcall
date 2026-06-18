using TotalCall.Core.Domain.Competitions;

namespace TotalCall.Client.Application.Providers;

// Loads competitions from the primary source (Supabase) first, falling back to the
// bundled JSON source when the primary has nothing published or is unavailable.
public sealed class CompositeCompetitionProvider(
    ICompetitionProvider primary,
    ICompetitionProvider fallback) : ICompetitionProvider
{
    public async Task<IReadOnlyList<CompetitionSummary>> GetCompetitionSummariesAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            var summaries = await primary.GetCompetitionSummariesAsync(cancellationToken);
            if (summaries.Count > 0)
            {
                return summaries;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Console.Error.WriteLine(
                $"[TotalCall] Remote competition list failed; using bundled data. {ex.Message}");
        }

        return await fallback.GetCompetitionSummariesAsync(cancellationToken);
    }

    public async Task<Competition?> GetCompetitionAsync(
        string slug,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var competition = await primary.GetCompetitionAsync(slug, cancellationToken);
            if (competition is not null)
            {
                return competition;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Console.Error.WriteLine(
                $"[TotalCall] Remote competition '{slug}' failed; using bundled data. {ex.Message}");
        }

        return await fallback.GetCompetitionAsync(slug, cancellationToken);
    }
}
