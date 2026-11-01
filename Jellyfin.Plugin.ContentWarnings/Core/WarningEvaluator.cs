namespace Jellyfin.Plugin.ContentWarnings.Core;

/// <summary>
/// A topic vote for one item.
/// </summary>
/// <param name="TopicId">DoesTheDogDie topic id.</param>
/// <param name="TopicName">DoesTheDogDie topic name.</param>
/// <param name="YesSum">Community "yes" votes.</param>
/// <param name="NoSum">Community "no" votes.</param>
/// <param name="DoesName">Question form, if known.</param>
/// <param name="IsSpoiler">Whether DoesTheDogDie flags the topic as a spoiler.</param>
public sealed record TopicVote(
    int TopicId,
    string TopicName,
    int YesSum,
    int NoSum,
    string? DoesName = null,
    bool IsSpoiler = false);

/// <summary>
/// An evaluated warning, ready to be shown or turned into a tag.
/// </summary>
/// <param name="TopicId">DoesTheDogDie topic id.</param>
/// <param name="Label">Short label.</param>
/// <param name="Tag">Full tag, including the configured prefix.</param>
/// <param name="Yes">Community "yes" votes.</param>
/// <param name="No">Community "no" votes.</param>
/// <param name="Ratio">yes / (yes + no).</param>
/// <param name="IsSpoiler">Whether the topic is a spoiler.</param>
public sealed record ContentWarning(
    int TopicId,
    string Label,
    string Tag,
    int Yes,
    int No,
    double Ratio,
    bool IsSpoiler);

/// <summary>
/// Evaluation settings, free of Jellyfin types.
/// </summary>
/// <param name="MinYesVotes">Minimum yes votes.</param>
/// <param name="MinYesRatio">Minimum yes ratio.</param>
/// <param name="EnabledTopicIds">Explicit topic ids; null or empty means use <see cref="DefaultTopics"/>.</param>
/// <param name="TopicLabelOverrides">Per-topic label overrides.</param>
/// <param name="IncludeSpoilerTopics">Whether spoiler topics are included.</param>
/// <param name="TagPrefix">Tag prefix.</param>
public sealed record WarningOptions(
    int MinYesVotes,
    double MinYesRatio,
    IReadOnlyCollection<int>? EnabledTopicIds,
    IReadOnlyDictionary<int, string>? TopicLabelOverrides,
    bool IncludeSpoilerTopics,
    string TagPrefix);

/// <summary>
/// Applies vote thresholds and topic filters.
/// </summary>
public static class WarningEvaluator
{
    /// <summary>
    /// Evaluates the votes of one item.
    /// </summary>
    /// <param name="votes">Topic votes from DoesTheDogDie.</param>
    /// <param name="options">Evaluation settings.</param>
    /// <returns>The warnings that pass every filter, ordered by label.</returns>
    public static IReadOnlyList<ContentWarning> Evaluate(IEnumerable<TopicVote> votes, WarningOptions options)
    {
        var result = new List<ContentWarning>();

        foreach (var vote in votes)
        {
            if (vote.YesSum < options.MinYesVotes)
            {
                continue;
            }

            var total = vote.YesSum + vote.NoSum;
            if (total <= 0)
            {
                continue;
            }

            var ratio = (double)vote.YesSum / total;
            if (ratio < options.MinYesRatio)
            {
                continue;
            }

            if (vote.IsSpoiler && !options.IncludeSpoilerTopics)
            {
                continue;
            }

            if (!IsEnabled(vote, options.EnabledTopicIds))
            {
                continue;
            }

            var label = TopicLabeler.ToLabel(
                vote.TopicId,
                vote.TopicName,
                vote.DoesName,
                options.TopicLabelOverrides);

            result.Add(new ContentWarning(
                vote.TopicId,
                label,
                options.TagPrefix + label,
                vote.YesSum,
                vote.NoSum,
                Math.Round(ratio, 4),
                vote.IsSpoiler));
        }

        return result
            .OrderBy(warning => warning.Label, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool IsEnabled(TopicVote vote, IReadOnlyCollection<int>? enabledTopicIds)
    {
        if (enabledTopicIds is { Count: > 0 })
        {
            return enabledTopicIds.Contains(vote.TopicId);
        }

        return DefaultTopics.IsDefault(vote.TopicName, vote.DoesName);
    }
}
