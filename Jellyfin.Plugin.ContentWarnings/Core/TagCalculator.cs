namespace Jellyfin.Plugin.ContentWarnings.Core;

/// <summary>
/// Merges plugin tags into an item without touching unrelated tags.
/// </summary>
public static class TagCalculator
{
    /// <summary>
    /// Removes every existing tag that starts with <paramref name="tagPrefix"/> and adds the desired tags.
    /// </summary>
    /// <param name="existingTags">Tags currently on the item.</param>
    /// <param name="desiredTags">Tags the plugin wants on the item.</param>
    /// <param name="tagPrefix">Plugin tag prefix.</param>
    /// <returns>The merged tag list. An empty prefix leaves the existing tags untouched.</returns>
    public static string[] Merge(IEnumerable<string>? existingTags, IEnumerable<string>? desiredTags, string tagPrefix)
    {
        var existing = (existingTags ?? []).Where(tag => !string.IsNullOrWhiteSpace(tag)).ToList();

        if (string.IsNullOrWhiteSpace(tagPrefix))
        {
            return existing.ToArray();
        }

        var merged = existing
            .Where(tag => !tag.StartsWith(tagPrefix, StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var desired in desiredTags ?? [])
        {
            if (string.IsNullOrWhiteSpace(desired))
            {
                continue;
            }

            if (!merged.Contains(desired, StringComparer.OrdinalIgnoreCase))
            {
                merged.Add(desired);
            }
        }

        return merged.ToArray();
    }

    /// <summary>
    /// Compares two tag lists as case-insensitive sets.
    /// </summary>
    /// <param name="left">First list.</param>
    /// <param name="right">Second list.</param>
    /// <returns>True when both contain the same tags.</returns>
    public static bool AreEqual(IEnumerable<string>? left, IEnumerable<string>? right)
    {
        var a = (left ?? []).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var b = (right ?? []).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return a.SetEquals(b);
    }
}
