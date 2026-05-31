namespace TotalCall.Client.Domain.Athletes;

/// <summary>How the athlete's recent form is trending, derived from real history metrics.</summary>
public enum AthleteFormPhase
{
    Unknown,
    Declining,
    Stable,
    Rising,
    Peak
}

/// <summary>Confidence that the athlete reaches their nominated total, derived from recent form vs nomination.</summary>
public enum AthleteRealizationTier
{
    Unknown,
    Low,
    Moderate,
    High
}

public enum AthleteVerdictTone
{
    Good,
    Caution
}

/// <summary>
/// Derived, presentation-only verdict over an athlete's real analytics. Carries neutral enums + a
/// meter level; components map these to localized labels. Never invents data — fields stay Unknown
/// when the underlying metrics are missing.
/// </summary>
public sealed record AthleteFormVerdict
{
    public static readonly AthleteFormVerdict Unavailable = new();

    public AthleteFormPhase Phase { get; init; } = AthleteFormPhase.Unknown;

    public AthleteRealizationTier Tier { get; init; } = AthleteRealizationTier.Unknown;

    public AthleteVerdictTone Tone { get; init; } = AthleteVerdictTone.Good;

    /// <summary>Filled meter segments out of 5. Zero means the meter has no data and is hidden.</summary>
    public int MeterOn { get; init; }

    public bool HasMeter => MeterOn > 0 && Tier != AthleteRealizationTier.Unknown;

    /// <summary>Whether there is enough signal to render the verdict banner at all.</summary>
    public bool IsAvailable => Phase != AthleteFormPhase.Unknown || Tier != AthleteRealizationTier.Unknown;
}

public enum AthleteSignalKind
{
    Trend,
    LastStart,
    AttemptSuccess,
    Stability
}

public enum AthleteSignalTone
{
    Up,
    Accent,
    Violet,
    Down
}

/// <summary>One "why this pick" signal chip. Raw values; components format + localize per kind.</summary>
public sealed record AthleteSignal
{
    public AthleteSignalKind Kind { get; init; }

    public AthleteSignalTone Tone { get; init; }

    /// <summary>Primary numeric value: signed kg (Trend, Stability) or percent (AttemptSuccess, LastStart % of PR).</summary>
    public decimal? Value { get; init; }

    /// <summary>Trend window length, in starts.</summary>
    public int? Count { get; init; }

    /// <summary>Last full-power start sits at or above the lifetime PR.</summary>
    public bool LastEqualsPr { get; init; }

    /// <summary>Secondary figure: last total as % of nomination (LastStart) or pp delta vs benchmark (AttemptSuccess).</summary>
    public decimal? Secondary { get; init; }
}
