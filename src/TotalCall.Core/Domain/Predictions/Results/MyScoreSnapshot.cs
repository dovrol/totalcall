namespace TotalCall.Core.Domain.Predictions.Results;

/// <summary>
/// The signed-in user's own score snapshot for one competition, as rendered by
/// the results board. Mirrors the backend <c>score_snapshots</c> projection
/// returned by the <c>get_my_score</c> RPC — the frontend never recomputes points.
/// </summary>
public sealed record MyScoreSnapshot
{
    public const string PartialStatus = "partial";
    public const string FinalStatus = "final";

    public int? Rank { get; init; }

    public decimal TotalPoints { get; init; }

    public int ScoredGroupsCount { get; init; }

    public int TotalGroupsCount { get; init; }

    public string Status { get; init; } = PartialStatus;

    public DateTimeOffset? LastCalculatedAt { get; init; }

    public IReadOnlyList<CategoryScoreBreakdown> Categories { get; init; } = [];

    public bool IsFinal => string.Equals(Status, FinalStatus, StringComparison.OrdinalIgnoreCase);

    public CategoryScoreBreakdown? FindCategory(string groupId, string questionId)
    {
        return Categories.FirstOrDefault(category =>
            string.Equals(category.GroupId, groupId, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(category.QuestionId, questionId, StringComparison.OrdinalIgnoreCase));
    }
}

/// <summary>One scored prediction group/category inside <see cref="MyScoreSnapshot"/>.</summary>
public sealed record CategoryScoreBreakdown
{
    public string GroupId { get; init; } = string.Empty;

    public string QuestionId { get; init; } = string.Empty;

    public string? CategoryId { get; init; }

    public decimal Points { get; init; }

    public decimal MaxPoints { get; init; }

    public decimal Placement { get; init; }

    public decimal PlacementMax { get; init; }

    public decimal SetBonus { get; init; }

    public decimal OrderBonus { get; init; }

    public string? Explanation { get; init; }

    public IReadOnlyList<CategorySlotResult> Slots { get; init; } = [];

    public IReadOnlyList<CategoryOfficialPlacement> Official { get; init; } = [];

    public CategorySlotResult? FindSlot(int position)
    {
        return Slots.FirstOrDefault(slot => slot.Position == position);
    }

    public CategoryOfficialPlacement? FindOfficial(int position)
    {
        return Official.FirstOrDefault(placement => placement.Position == position);
    }
}

/// <summary>Per-pick verdict for one slot (exact / wrong / miss / withdrawn).</summary>
public sealed record CategorySlotResult
{
    public int Position { get; init; }

    public string AthleteId { get; init; } = string.Empty;

    public string Verdict { get; init; } = string.Empty;

    public decimal Points { get; init; }
}

/// <summary>One official finishing placement imported for a category.</summary>
public sealed record CategoryOfficialPlacement
{
    public int Position { get; init; }

    public string AthleteId { get; init; } = string.Empty;

    /// <summary>The athlete's actual official squat (kg), when imported.</summary>
    public decimal? SquatKg { get; init; }

    /// <summary>The athlete's actual official bench (kg), when imported.</summary>
    public decimal? BenchKg { get; init; }

    /// <summary>The athlete's actual official deadlift (kg), when imported.</summary>
    public decimal? DeadliftKg { get; init; }

    /// <summary>The athlete's actual official total (kg), when imported.</summary>
    public decimal? TotalKg { get; init; }
}

/// <summary>Stable verdict identifiers shared with the backend snapshot and the results UI.</summary>
public static class ResultVerdict
{
    public const string Exact = "exact";
    public const string Wrong = "wrong";
    public const string Miss = "miss";
    public const string Withdrawn = "withdrawn";
}
