using System.Globalization;

namespace TotalCall.Core.Domain.Releases;

/// <summary>
/// Minimal SemVer 2.0 comparator covering the formats used by TotalCall:
/// MAJOR.MINOR.PATCH and MAJOR.MINOR.PATCH-PRERELEASE (e.g. "0.1.0-beta.1").
/// Build metadata after '+' (e.g. "+1a2b3c4") is ignored for comparison.
/// </summary>
public readonly record struct SemanticVersion(
    int Major,
    int Minor,
    int Patch,
    IReadOnlyList<string> PreRelease) : IComparable<SemanticVersion>
{
    public bool IsPreRelease => PreRelease.Count > 0;

    public static bool TryParse(string? value, out SemanticVersion version)
    {
        version = default;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim();

        if (trimmed.StartsWith('v'))
        {
            trimmed = trimmed[1..];
        }

        var plusIndex = trimmed.IndexOf('+');

        if (plusIndex >= 0)
        {
            trimmed = trimmed[..plusIndex];
        }

        var dashIndex = trimmed.IndexOf('-');
        var core = dashIndex >= 0 ? trimmed[..dashIndex] : trimmed;
        var pre = dashIndex >= 0 ? trimmed[(dashIndex + 1)..] : string.Empty;

        var parts = core.Split('.');

        if (parts.Length != 3)
        {
            return false;
        }

        if (!int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var major) ||
            !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var minor) ||
            !int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var patch))
        {
            return false;
        }

        var preIdentifiers = string.IsNullOrEmpty(pre)
            ? Array.Empty<string>()
            : pre.Split('.');

        version = new SemanticVersion(major, minor, patch, preIdentifiers);

        return true;
    }

    public int CompareTo(SemanticVersion other)
    {
        var coreComparison = (Major, Minor, Patch).CompareTo((other.Major, other.Minor, other.Patch));

        if (coreComparison != 0)
        {
            return coreComparison;
        }

        // SemVer rule: a version without prerelease is greater than one with prerelease.
        if (PreRelease.Count == 0 && other.PreRelease.Count == 0)
        {
            return 0;
        }

        if (PreRelease.Count == 0)
        {
            return 1;
        }

        if (other.PreRelease.Count == 0)
        {
            return -1;
        }

        var minLength = Math.Min(PreRelease.Count, other.PreRelease.Count);

        for (var i = 0; i < minLength; i++)
        {
            var left = PreRelease[i];
            var right = other.PreRelease[i];

            var leftIsNumeric = int.TryParse(left, NumberStyles.Integer, CultureInfo.InvariantCulture, out var leftNumber);
            var rightIsNumeric = int.TryParse(right, NumberStyles.Integer, CultureInfo.InvariantCulture, out var rightNumber);

            if (leftIsNumeric && rightIsNumeric)
            {
                var numericComparison = leftNumber.CompareTo(rightNumber);

                if (numericComparison != 0)
                {
                    return numericComparison;
                }

                continue;
            }

            if (leftIsNumeric)
            {
                return -1;
            }

            if (rightIsNumeric)
            {
                return 1;
            }

            var stringComparison = string.CompareOrdinal(left, right);

            if (stringComparison != 0)
            {
                return stringComparison;
            }
        }

        return PreRelease.Count.CompareTo(other.PreRelease.Count);
    }
}
