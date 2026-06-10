using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace TotalCall.Sync;

// Mirrors public.normalize_name() in supabase/migrations/20260527180000_create_athlete_data_backend.sql.
// Steps: unaccent -> lower -> non-alphanumeric to single space -> collapse whitespace -> trim.
// IMPORTANT: keep behavior in sync with the SQL function. If you change one, change both.
public static class NameNormalizer
{
    private static readonly Regex NonAlphaNumeric = new(@"[^a-z0-9]+", RegexOptions.Compiled);
    private static readonly Regex Whitespace = new(@"\s+", RegexOptions.Compiled);

    public static string? Normalize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return null;
        }

        var unaccented = Unaccent(input);
        var lower = unaccented.ToLowerInvariant();
        var alphaOnly = NonAlphaNumeric.Replace(lower, " ");
        var collapsed = Whitespace.Replace(alphaOnly, " ").Trim();
        return collapsed.Length == 0 ? null : collapsed;
    }

    private static string Unaccent(string input)
    {
        var normalized = input.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        foreach (var c in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(c);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }
}
