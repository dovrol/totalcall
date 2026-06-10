using TotalCall.Client.Application.Providers;
using TotalCall.Client.Domain.Competitions;

namespace TotalCall.Tests.Application.Providers;

public sealed class CompositeCompetitionProviderTests
{
    [Fact]
    public async Task GetSummaries_WhenPrimaryHasData_UsesPrimaryAndSkipsFallback()
    {
        var primary = new FakeProvider(summaries: () => [Summary("worlds-2026")]);
        var fallback = new FakeProvider(summaries: () => [Summary("fallback")]);
        var provider = new CompositeCompetitionProvider(primary, fallback);

        var result = await provider.GetCompetitionSummariesAsync();

        Assert.Equal("worlds-2026", Assert.Single(result).Id);
        Assert.Equal(1, primary.SummaryCalls);
        Assert.Equal(0, fallback.SummaryCalls);
    }

    [Fact]
    public async Task GetSummaries_WhenPrimaryEmpty_FallsBack()
    {
        var primary = new FakeProvider(summaries: () => []);
        var fallback = new FakeProvider(summaries: () => [Summary("fallback")]);
        var provider = new CompositeCompetitionProvider(primary, fallback);

        var result = await provider.GetCompetitionSummariesAsync();

        Assert.Equal("fallback", Assert.Single(result).Id);
        Assert.Equal(1, fallback.SummaryCalls);
    }

    [Fact]
    public async Task GetSummaries_WhenPrimaryThrows_FallsBack()
    {
        var primary = new FakeProvider(summaries: () => throw new HttpRequestException("boom"));
        var fallback = new FakeProvider(summaries: () => [Summary("fallback")]);
        var provider = new CompositeCompetitionProvider(primary, fallback);

        var result = await provider.GetCompetitionSummariesAsync();

        Assert.Equal("fallback", Assert.Single(result).Id);
    }

    [Fact]
    public async Task GetCompetition_WhenPrimaryReturns_UsesPrimary()
    {
        var primary = new FakeProvider(competition: () => Competition("worlds-2026"));
        var fallback = new FakeProvider(competition: () => Competition("fallback"));
        var provider = new CompositeCompetitionProvider(primary, fallback);

        var result = await provider.GetCompetitionAsync("worlds-2026");

        Assert.Equal("worlds-2026", result?.Id);
        Assert.Equal(0, fallback.CompetitionCalls);
    }

    [Fact]
    public async Task GetCompetition_WhenPrimaryNull_FallsBack()
    {
        var primary = new FakeProvider(competition: () => null);
        var fallback = new FakeProvider(competition: () => Competition("fallback"));
        var provider = new CompositeCompetitionProvider(primary, fallback);

        var result = await provider.GetCompetitionAsync("worlds-2026");

        Assert.Equal("fallback", result?.Id);
        Assert.Equal(1, fallback.CompetitionCalls);
    }

    [Fact]
    public async Task GetCompetition_WhenPrimaryThrows_FallsBack()
    {
        var primary = new FakeProvider(competition: () => throw new HttpRequestException("boom"));
        var fallback = new FakeProvider(competition: () => Competition("fallback"));
        var provider = new CompositeCompetitionProvider(primary, fallback);

        var result = await provider.GetCompetitionAsync("worlds-2026");

        Assert.Equal("fallback", result?.Id);
    }

    private static CompetitionSummary Summary(string id) => new()
    {
        Id = id,
        Slug = id,
        Name = id,
        ConfigVersion = "1"
    };

    private static Competition Competition(string id) => new()
    {
        Id = id,
        Slug = id,
        Name = id,
        ConfigVersion = "1"
    };

    private sealed class FakeProvider(
        Func<IReadOnlyList<CompetitionSummary>>? summaries = null,
        Func<Competition?>? competition = null) : ICompetitionProvider
    {
        public int SummaryCalls { get; private set; }

        public int CompetitionCalls { get; private set; }

        public Task<IReadOnlyList<CompetitionSummary>> GetCompetitionSummariesAsync(
            CancellationToken cancellationToken = default)
        {
            SummaryCalls++;
            return Task.FromResult(
                summaries?.Invoke() ?? throw new InvalidOperationException("summaries not configured"));
        }

        public Task<Competition?> GetCompetitionAsync(
            string slug,
            CancellationToken cancellationToken = default)
        {
            CompetitionCalls++;
            return Task.FromResult(
                competition is null ? throw new InvalidOperationException("competition not configured") : competition());
        }
    }
}
