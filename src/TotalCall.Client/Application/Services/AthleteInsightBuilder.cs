using TotalCall.Client.Domain.Athletes;

namespace TotalCall.Client.Application.Services;

/// <summary>
/// Derives the athlete-profile verdict + "why this pick" signals from already-computed analytics.
/// Pure presentation logic over real data (no new API calls, no fabricated metrics): everything
/// here is a transparent interpretation of fields the app already exposes.
/// </summary>
public static class AthleteInsightBuilder
{
    /// <summary>A benchmark needs at least this many counted attempts before its delta is trustworthy.</summary>
    public const int MinimumBenchmarkCountedAttempts = 30;

    /// <summary>Last total within this % of the lifetime best counts as "at PR".</summary>
    private const decimal AtPrPercent = 99.5m;

    private const decimal NearNominationRatio = 0.985m;
    private const decimal ReliableAttemptRate = 85m;
    private const decimal RiskyAttemptRate = 60m;

    public static AthleteFormVerdict BuildVerdict(AthleteAnalytics? analytics, decimal? nominatedKg)
    {
        if (analytics is null)
        {
            return AthleteFormVerdict.Unavailable;
        }

        var recentForm = analytics.LastTotalKg ?? analytics.Last3AvgTotalKg ?? analytics.BestTotalKg;
        var trendKg = analytics.RecentTotalTrendKg ?? analytics.TotalTrendKg;
        var atOrAbovePr = analytics.LastTotalToBestPercent is >= AtPrPercent;
        var trendUp = trendKg is > 0m;
        var trendDown = trendKg is < 0m;
        var overallRate = analytics.OverallAttempts.RatePercent;

        var phase = ResolvePhase(recentForm, trendKg, atOrAbovePr, trendUp, trendDown);
        var (tier, meterOn) = ResolveRealization(
            recentForm, nominatedKg, trendUp, trendDown, atOrAbovePr, overallRate);

        var tone = tier == AthleteRealizationTier.Low || phase == AthleteFormPhase.Declining
            ? AthleteVerdictTone.Caution
            : AthleteVerdictTone.Good;

        return new AthleteFormVerdict
        {
            Phase = phase,
            Tier = tier,
            Tone = tone,
            MeterOn = meterOn
        };
    }

    public static IReadOnlyList<AthleteSignal> BuildSignals(
        AthleteAnalytics? analytics,
        AthleteAttemptBenchmark? benchmark,
        decimal? nominatedKg)
    {
        if (analytics is null)
        {
            return [];
        }

        var signals = new List<AthleteSignal>(4);

        var trendKg = analytics.RecentTotalTrendKg ?? analytics.TotalTrendKg;
        if (trendKg is { } trend)
        {
            signals.Add(new AthleteSignal
            {
                Kind = AthleteSignalKind.Trend,
                Tone = trend > 0m ? AthleteSignalTone.Up : trend < 0m ? AthleteSignalTone.Down : AthleteSignalTone.Violet,
                Value = trend,
                Count = analytics.RecentTotalTrendStarts
            });
        }

        if (analytics.LastTotalKg is { } last && analytics.BestTotalKg is not null)
        {
            signals.Add(new AthleteSignal
            {
                Kind = AthleteSignalKind.LastStart,
                Tone = AthleteSignalTone.Accent,
                Value = analytics.LastTotalToBestPercent,
                LastEqualsPr = analytics.LastTotalToBestPercent is >= AtPrPercent,
                Secondary = nominatedKg is > 0m ? Math.Round(100m * last / nominatedKg.Value, 0) : null
            });
        }

        if (analytics.OverallAttempts.RatePercent is { } rate)
        {
            signals.Add(new AthleteSignal
            {
                Kind = AthleteSignalKind.AttemptSuccess,
                Tone = rate >= ReliableAttemptRate ? AthleteSignalTone.Up : AthleteSignalTone.Violet,
                Value = rate,
                Secondary = ResolveBenchmarkDeltaPp(analytics.OverallAttempts, benchmark?.OverallAttempts)
            });
        }

        if (analytics.TotalStabilityKg is { } stability)
        {
            signals.Add(new AthleteSignal
            {
                Kind = AthleteSignalKind.Stability,
                Tone = AthleteSignalTone.Violet,
                Value = stability
            });
        }

        return signals;
    }

    /// <summary>Percentage-point delta of an athlete's attempt rate vs a benchmark, or null when not comparable.</summary>
    public static decimal? ResolveBenchmarkDeltaPp(
        AthleteAttemptSuccessRate attempts,
        AthleteAttemptSuccessRate? benchmarkAttempts)
    {
        if (attempts.RatePercent is null ||
            benchmarkAttempts?.RatePercent is null ||
            benchmarkAttempts.CountedAttempts < MinimumBenchmarkCountedAttempts)
        {
            return null;
        }

        return attempts.RatePercent.Value - benchmarkAttempts.RatePercent.Value;
    }

    private static AthleteFormPhase ResolvePhase(
        decimal? recentForm,
        decimal? trendKg,
        bool atOrAbovePr,
        bool trendUp,
        bool trendDown)
    {
        if (atOrAbovePr && !trendDown)
        {
            return AthleteFormPhase.Peak;
        }

        if (trendUp)
        {
            return AthleteFormPhase.Rising;
        }

        if (trendDown)
        {
            return AthleteFormPhase.Declining;
        }

        return recentForm is not null || trendKg is not null
            ? AthleteFormPhase.Stable
            : AthleteFormPhase.Unknown;
    }

    private static (AthleteRealizationTier Tier, int MeterOn) ResolveRealization(
        decimal? recentForm,
        decimal? nominatedKg,
        bool trendUp,
        bool trendDown,
        bool atOrAbovePr,
        decimal? overallRate)
    {
        if (recentForm is null || nominatedKg is not > 0m)
        {
            return (AthleteRealizationTier.Unknown, 0);
        }

        var ratio = recentForm.Value / nominatedKg.Value;
        var score = ratio >= 1m ? 2 : ratio >= NearNominationRatio ? 1 : 0;

        if (trendUp)
        {
            score += 1;
        }
        else if (trendDown)
        {
            score -= 1;
        }

        if (atOrAbovePr)
        {
            score += 1;
        }

        if (overallRate >= ReliableAttemptRate)
        {
            score += 1;
        }
        else if (overallRate is not null && overallRate < RiskyAttemptRate)
        {
            score -= 1;
        }

        score = Math.Clamp(score, 0, 5);

        var tier = score >= 4
            ? AthleteRealizationTier.High
            : score == 3
                ? AthleteRealizationTier.Moderate
                : AthleteRealizationTier.Low;

        return (tier, Math.Max(1, score));
    }
}
