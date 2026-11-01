using System.Globalization;

namespace Jellyfin.Plugin.ContentWarnings.Core;

/// <summary>
/// Turns DoesTheDogDie questions into short, stable tag labels.
/// </summary>
public static class TopicLabeler
{
    private static readonly string[] QuestionPrefixes =
    [
        "does the ", "do the ", "is there ", "are there ", "does ", "do ", "is ", "are ",
        "will ", "can ", "has ", "have ", "was ", "were ", "there is ", "there are ", "there's "
    ];

    private static readonly string[] LeadingArticles =
    [
        "the ", "a ", "an ", "el ", "la ", "los ", "las "
    ];

    /// <summary>
    /// Builds the tag for a topic.
    /// </summary>
    /// <param name="topicId">DoesTheDogDie topic id.</param>
    /// <param name="topicName">DoesTheDogDie topic name.</param>
    /// <param name="doesName">DoesTheDogDie question form, if known.</param>
    /// <param name="overrides">Per-topic overrides from the plugin configuration.</param>
    /// <param name="tagPrefix">Configured tag prefix.</param>
    /// <returns>The full tag.</returns>
    public static string ToTag(
        int topicId,
        string? topicName,
        string? doesName,
        IReadOnlyDictionary<int, string>? overrides,
        string tagPrefix)
    {
        return tagPrefix + ToLabel(topicId, topicName, doesName, overrides);
    }

    /// <summary>
    /// Builds the label for a topic.
    /// </summary>
    /// <param name="topicId">DoesTheDogDie topic id.</param>
    /// <param name="topicName">DoesTheDogDie topic name.</param>
    /// <param name="doesName">DoesTheDogDie question form, if known.</param>
    /// <param name="overrides">Per-topic overrides from the plugin configuration.</param>
    /// <returns>The label without the tag prefix.</returns>
    public static string ToLabel(
        int topicId,
        string? topicName,
        string? doesName,
        IReadOnlyDictionary<int, string>? overrides)
    {
        if (overrides is not null
            && overrides.TryGetValue(topicId, out var custom)
            && !string.IsNullOrWhiteSpace(custom))
        {
            return custom.Trim();
        }

        var curated = DefaultTopics.GetLabel(topicName, doesName);
        if (curated is not null)
        {
            return curated;
        }

        var fallback = Shorten(doesName);
        if (fallback.Length == 0)
        {
            fallback = Shorten(topicName);
        }

        return fallback.Length > 0 ? fallback : $"Topic {topicId}";
    }

    private static string Shorten(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var text = value.Trim().TrimEnd('?', '.', '!').Trim();
        var changed = true;
        while (changed)
        {
            changed = false;
            foreach (var prefix in QuestionPrefixes)
            {
                if (text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    text = text[prefix.Length..].Trim();
                    changed = true;
                    break;
                }
            }
        }

        if (text.Length == 0)
        {
            return string.Empty;
        }

        foreach (var article in LeadingArticles)
        {
            if (text.Length > article.Length && text.StartsWith(article, StringComparison.OrdinalIgnoreCase))
            {
                text = text[article.Length..];
                break;
            }
        }

        return char.ToUpper(text[0], CultureInfo.InvariantCulture) + text[1..];
    }
}
