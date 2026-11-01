using System.Security.Cryptography;
using System.Text;

namespace Jellyfin.Plugin.ContentWarnings.Core;

/// <summary>
/// Decides whether an item gets the poster badge and fingerprints what the badge shows.
/// </summary>
public static class BadgeDecision
{
    /// <summary>
    /// Bump when the badge artwork changes, so existing badges are redrawn.
    /// </summary>
    public const int Version = 3;

    /// <summary>
    /// Gets whether the item carries at least one tag written by this plugin.
    /// </summary>
    /// <param name="enabled">Whether the feature is enabled in the configuration.</param>
    /// <param name="tagPrefix">Configured tag prefix.</param>
    /// <param name="tags">Tags to inspect.</param>
    /// <returns>True when a badge is wanted.</returns>
    public static bool ShouldBadge(bool enabled, string? tagPrefix, IReadOnlyCollection<string>? tags)
        => enabled
           && !string.IsNullOrWhiteSpace(tagPrefix)
           && tags is not null
           && tags.Any(tag => tag.StartsWith(tagPrefix, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Builds a stable fingerprint of everything that changes the rendered badge.
    /// </summary>
    /// <param name="tagPrefix">Configured tag prefix.</param>
    /// <param name="corner">Configured badge corner.</param>
    /// <param name="tags">Content warning tags.</param>
    /// <returns>Hex fingerprint.</returns>
    public static string Fingerprint(string tagPrefix, string corner, IEnumerable<string> tags)
    {
        var text = Version + "\n" + tagPrefix + "\n" + corner + "\n"
                   + string.Join('\n', tags.OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    }
}
