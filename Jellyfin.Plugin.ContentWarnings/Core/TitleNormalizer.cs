using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Jellyfin.Plugin.ContentWarnings.Core;

/// <summary>
/// Normalizes titles for comparison.
/// </summary>
public static partial class TitleNormalizer
{
    private static readonly string[] LeadingArticles =
    [
        "the", "a", "an", "el", "la", "los", "las", "le", "les", "il", "lo", "der", "die", "das"
    ];

    /// <summary>
    /// Lowercases, removes diacritics and punctuation, collapses whitespace and drops a leading article.
    /// </summary>
    /// <param name="title">The title to normalize.</param>
    /// <returns>The normalized title, or an empty string.</returns>
    public static string Normalize(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return string.Empty;
        }

        var decomposed = title.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(character);
            }
        }

        var lowered = builder.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant();
        lowered = NonAlphaNumeric().Replace(lowered, " ");
        lowered = Whitespace().Replace(lowered, " ").Trim();

        foreach (var article in LeadingArticles)
        {
            if (lowered.Length > article.Length
                && lowered.StartsWith(article + " ", StringComparison.Ordinal))
            {
                lowered = lowered[(article.Length + 1)..];
                break;
            }
        }

        return lowered;
    }

    /// <summary>
    /// Returns the normalized Levenshtein similarity of two strings, from 0 to 1.
    /// </summary>
    /// <param name="left">First string.</param>
    /// <param name="right">Second string.</param>
    /// <returns>1 for equal strings, 0 for completely different ones.</returns>
    public static double Similarity(string? left, string? right)
    {
        var a = Normalize(left);
        var b = Normalize(right);

        if (a.Length == 0 || b.Length == 0)
        {
            return a.Length == b.Length ? 1 : 0;
        }

        var distance = Levenshtein(a, b);
        return 1.0 - ((double)distance / Math.Max(a.Length, b.Length));
    }

    private static int Levenshtein(string a, string b)
    {
        var previous = new int[b.Length + 1];
        var current = new int[b.Length + 1];

        for (var j = 0; j <= b.Length; j++)
        {
            previous[j] = j;
        }

        for (var i = 1; i <= a.Length; i++)
        {
            current[0] = i;
            for (var j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                current[j] = Math.Min(
                    Math.Min(current[j - 1] + 1, previous[j] + 1),
                    previous[j - 1] + cost);
            }

            (previous, current) = (current, previous);
        }

        return previous[b.Length];
    }

    [GeneratedRegex(@"[^\p{L}\p{N}\s]")]
    private static partial Regex NonAlphaNumeric();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
