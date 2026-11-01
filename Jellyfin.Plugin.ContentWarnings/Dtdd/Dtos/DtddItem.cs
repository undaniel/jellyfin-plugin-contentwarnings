namespace Jellyfin.Plugin.ContentWarnings.Dtdd.Dtos;

/// <summary>
/// A search result from GET /api/v3/items.
/// </summary>
public class DtddItem
{
    /// <summary>Gets or sets the item id.</summary>
    public int Id { get; set; }

    /// <summary>Gets or sets the title.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the release year.</summary>
    public int? ReleaseYear { get; set; }

    /// <summary>Gets or sets the item type name ("Movie", "TV", ...).</summary>
    public string? ItemTypeName { get; set; }

    /// <summary>Gets or sets the TMDb id.</summary>
    public int? TmdbId { get; set; }

    /// <summary>Gets or sets the IMDb id.</summary>
    public string? ImdbId { get; set; }
}

/// <summary>
/// Full item detail from GET /api/v3/items/{id}.
/// </summary>
public class DtddItemDetail : DtddItem
{
    /// <summary>Gets or sets the rolled-up topic votes.</summary>
    public List<DtddTopicStat> TopicItemStats { get; set; } = [];
}

/// <summary>
/// One topic's yes/no totals for an item.
/// </summary>
public class DtddTopicStat
{
    /// <summary>Gets or sets the topic id.</summary>
    public int TopicId { get; set; }

    /// <summary>Gets or sets the topic name.</summary>
    public string TopicName { get; set; } = string.Empty;

    /// <summary>Gets or sets the "yes" votes.</summary>
    public int YesSum { get; set; }

    /// <summary>Gets or sets the "no" votes.</summary>
    public int NoSum { get; set; }
}

/// <summary>
/// A topic from GET /api/v3/topics.
/// </summary>
public class DtddTopic
{
    /// <summary>Gets or sets the topic id.</summary>
    public int Id { get; set; }

    /// <summary>Gets or sets the statement form, for example "a dog dies".</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the question form.</summary>
    public string? DoesName { get; set; }

    /// <summary>Gets or sets the comma-separated keywords.</summary>
    public string? Keywords { get; set; }

    /// <summary>Gets or sets the list label.</summary>
    public string? ListName { get; set; }

    /// <summary>Gets or sets the minimal label.</summary>
    public string? MinimalName { get; set; }

    /// <summary>Gets or sets the category id.</summary>
    public int? TopicCategoryId { get; set; }
}
