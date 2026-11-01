namespace Jellyfin.Plugin.ContentWarnings.Api;

/// <summary>
/// One warning in an API response.
/// </summary>
/// <param name="TopicId">DoesTheDogDie topic id.</param>
/// <param name="Label">Short label.</param>
/// <param name="Yes">Yes votes.</param>
/// <param name="No">No votes.</param>
/// <param name="Ratio">Yes ratio.</param>
/// <param name="IsSpoiler">Whether the topic is a spoiler.</param>
public sealed record WarningResponse(int TopicId, string Label, int Yes, int No, double Ratio, bool IsSpoiler);

/// <summary>
/// Response of GET /ContentWarnings/Items/{itemId}.
/// </summary>
/// <param name="ItemId">Jellyfin item id.</param>
/// <param name="DtddId">DoesTheDogDie id, if matched.</param>
/// <param name="DtddUrl">Public DoesTheDogDie URL, if matched.</param>
/// <param name="MatchMethod">How the item was matched.</param>
/// <param name="FetchedAt">When the cached payload was fetched.</param>
/// <param name="Warnings">Evaluated warnings.</param>
/// <param name="Attribution">Required attribution text.</param>
public sealed record ItemWarningsResponse(
    Guid ItemId,
    int? DtddId,
    string? DtddUrl,
    string MatchMethod,
    DateTimeOffset? FetchedAt,
    IReadOnlyList<WarningResponse> Warnings,
    string Attribution);

/// <summary>
/// An item with no match.
/// </summary>
/// <param name="ItemId">Jellyfin item id.</param>
/// <param name="Name">Item name.</param>
/// <param name="Year">Production year.</param>
/// <param name="Type">Movie or Series.</param>
/// <param name="CheckedAt">When the match was last attempted.</param>
public sealed record UnmatchedItemResponse(Guid ItemId, string Name, int? Year, string Type, DateTimeOffset CheckedAt);

/// <summary>
/// A topic shown on the configuration page.
/// </summary>
/// <param name="Id">Topic id.</param>
/// <param name="Name">Statement form.</param>
/// <param name="DoesName">Question form.</param>
/// <param name="Keywords">Keywords.</param>
/// <param name="IsDefault">Whether the topic is in the curated defaults.</param>
public sealed record TopicResponse(int Id, string Name, string? DoesName, string? Keywords, bool IsDefault);

/// <summary>
/// Body of PUT /ContentWarnings/Items/{itemId}/Match.
/// </summary>
/// <param name="DtddId">Manual id, or null for "no match, do not retry".</param>
public sealed record ManualMatchRequest(int? DtddId);

/// <summary>
/// Result of POST /ContentWarnings/TestKey.
/// </summary>
/// <param name="Ok">Whether the key works.</param>
/// <param name="Message">Human-readable result.</param>
public sealed record TestConnectionResponse(bool Ok, string Message);
