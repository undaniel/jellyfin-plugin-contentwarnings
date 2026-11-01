using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.ContentWarnings.Configuration;

/// <summary>
/// One per-topic tag label override. A list is used instead of a dictionary because
/// Jellyfin serializes plugin configuration with <see cref="System.Xml.Serialization.XmlSerializer"/>,
/// which cannot serialize dictionaries.
/// </summary>
public class TopicLabelOverride
{
    /// <summary>
    /// Gets or sets the DoesTheDogDie topic id.
    /// </summary>
    public int TopicId { get; set; }

    /// <summary>
    /// Gets or sets the custom label.
    /// </summary>
    public string Label { get; set; } = string.Empty;
}

/// <summary>
/// Plugin configuration.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Gets or sets the personal DoesTheDogDie API key. Required.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the prefix of every tag written by this plugin.
    /// </summary>
    public string TagPrefix { get; set; } = "CW: ";

    /// <summary>
    /// Gets or sets the minimum number of "yes" votes for a topic to be applied.
    /// </summary>
    public int MinYesVotes { get; set; } = 3;

    /// <summary>
    /// Gets or sets the minimum yes / (yes + no) ratio for a topic to be applied.
    /// </summary>
    public double MinYesRatio { get; set; } = 0.6;

    /// <summary>
    /// Gets or sets the explicitly enabled topic ids. Empty means "use the curated defaults".
    /// </summary>
    public List<int> EnabledTopicIds { get; set; } = [];

    /// <summary>
    /// Gets or sets per-topic label overrides, keyed by DoesTheDogDie topic id.
    /// </summary>
    public List<TopicLabelOverride> TopicLabelOverrides { get; set; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether topics flagged as spoilers should be applied.
    /// </summary>
    public bool IncludeSpoilerTopics { get; set; }

    /// <summary>
    /// Gets or sets the library ids to process. Empty means all movie and series libraries.
    /// </summary>
    public List<Guid> LibraryIds { get; set; } = [];

    /// <summary>
    /// Gets or sets how many days a fetched media payload stays fresh.
    /// </summary>
    public int CacheTtlDays { get; set; } = 30;

    /// <summary>
    /// Gets or sets how many days to wait before retrying an item with no match.
    /// </summary>
    public int NotFoundRetryDays { get; set; } = 14;

    /// <summary>
    /// Gets or sets the maximum number of requests per second sent to DoesTheDogDie.
    /// </summary>
    public double RequestsPerSecond { get; set; } = 1.0;

    /// <summary>
    /// Gets or sets a value indicating whether newly added items are processed automatically.
    /// </summary>
    public bool ProcessOnItemAdded { get; set; } = true;
}
