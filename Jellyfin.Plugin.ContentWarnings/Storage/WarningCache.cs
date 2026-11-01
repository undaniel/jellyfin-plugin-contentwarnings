using System.Collections.Concurrent;
using System.Text.Json;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.ContentWarnings.Storage;

/// <summary>
/// How an item was matched.
/// </summary>
public static class MatchMethods
{
    /// <summary>Matched by IMDb id.</summary>
    public const string Imdb = "Imdb";

    /// <summary>Matched by TMDb id.</summary>
    public const string Tmdb = "Tmdb";

    /// <summary>Matched by title and year.</summary>
    public const string Title = "Title";

    /// <summary>Set by the administrator.</summary>
    public const string Manual = "Manual";

    /// <summary>No safe match; retried after the configured delay.</summary>
    public const string NoMatch = "NoMatch";
}

/// <summary>
/// Cached match state for one Jellyfin item.
/// </summary>
public sealed class MatchEntry
{
    /// <summary>Gets or sets the DoesTheDogDie id. Null means no match.</summary>
    public int? DtddId { get; set; }

    /// <summary>Gets or sets the match method.</summary>
    public string Method { get; set; } = MatchMethods.NoMatch;

    /// <summary>Gets or sets when the match was last checked.</summary>
    public DateTimeOffset CheckedAt { get; set; }
}

/// <summary>
/// One cached topic vote.
/// </summary>
public sealed class CachedTopicVote
{
    /// <summary>Gets or sets the topic id.</summary>
    public int Id { get; set; }

    /// <summary>Gets or sets the topic name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the question form.</summary>
    public string? DoesName { get; set; }

    /// <summary>Gets or sets whether the topic is a spoiler.</summary>
    public bool IsSpoiler { get; set; }

    /// <summary>Gets or sets the yes votes.</summary>
    public int YesSum { get; set; }

    /// <summary>Gets or sets the no votes.</summary>
    public int NoSum { get; set; }
}

/// <summary>
/// One image of an item that carries the badge.
/// </summary>
public sealed class BadgedImage
{
    /// <summary>Gets or sets the image type.</summary>
    public ImageType Type { get; set; }

    /// <summary>Gets or sets the image index.</summary>
    public int Index { get; set; }

    /// <summary>Gets or sets the image path the item had before the badge was applied.</summary>
    public string OriginalPath { get; set; } = string.Empty;

    /// <summary>Gets or sets the badged image path.</summary>
    public string BadgedPath { get; set; } = string.Empty;
}

/// <summary>
/// Badged images currently applied to one item.
/// </summary>
public sealed class BadgeEntry
{
    /// <summary>Gets or sets the fingerprint of the rendered badge.</summary>
    public string Hash { get; set; } = string.Empty;

    /// <summary>Gets or sets the images that carry the badge.</summary>
    public List<BadgedImage> Images { get; set; } = [];

    /// <summary>
    /// Gets or sets the original path written by plugin 1.0.0.0, which only badged Primary.
    /// Read on load to migrate it into <see cref="Images"/>; empty afterwards.
    /// </summary>
    public string OriginalPath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the badged path written by plugin 1.0.0.0, which only badged Primary.
    /// Read on load to migrate it into <see cref="Images"/>; empty afterwards.
    /// </summary>
    public string BadgedPath { get; set; } = string.Empty;
}

/// <summary>
/// Reduced media payload kept on disk.
/// </summary>
public sealed class CachedMedia
{
    /// <summary>Gets or sets the DoesTheDogDie id.</summary>
    public int DtddId { get; set; }

    /// <summary>Gets or sets when the payload was fetched.</summary>
    public DateTimeOffset FetchedAt { get; set; }

    /// <summary>Gets or sets the topic votes. Comments are never stored.</summary>
    public List<CachedTopicVote> Topics { get; set; } = [];
}

/// <summary>
/// One topic from the cached taxonomy.
/// </summary>
public sealed class CachedTopicInfo
{
    /// <summary>Gets or sets the topic id.</summary>
    public int Id { get; set; }

    /// <summary>Gets or sets the topic name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the question form.</summary>
    public string? DoesName { get; set; }

    /// <summary>Gets or sets the keywords.</summary>
    public string? Keywords { get; set; }

    /// <summary>Gets or sets the category id.</summary>
    public int? TopicCategoryId { get; set; }
}

/// <summary>
/// Cached topic taxonomy.
/// </summary>
public sealed class TopicsCache
{
    /// <summary>Gets or sets when the taxonomy was fetched.</summary>
    public DateTimeOffset FetchedAt { get; set; }

    /// <summary>Gets or sets the topics.</summary>
    public List<CachedTopicInfo> Topics { get; set; } = [];
}

/// <summary>
/// JSON file cache under {DataPath}/contentwarnings.
/// </summary>
/// <remarks>
/// shortcut: matches.json is rewritten whole and media files are cached in memory; migrate to
/// SQLite if a library grows beyond roughly 50k items.
/// </remarks>
public sealed class WarningCache
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _directory;
    private readonly string _matchesPath;
    private readonly string _mediaDirectory;
    private readonly string _topicsPath;
    private readonly string _badgesPath;
    private readonly ILogger<WarningCache> _logger;
    private readonly SemaphoreSlim _ioGate = new(1, 1);
    private readonly ConcurrentDictionary<Guid, MatchEntry> _matches = new();
    private readonly ConcurrentDictionary<int, CachedMedia> _media = new();
    private readonly ConcurrentDictionary<Guid, BadgeEntry> _badges = new();
    private TopicsCache? _topics;

    /// <summary>
    /// Initializes a new instance of the <see cref="WarningCache"/> class.
    /// </summary>
    /// <param name="directory">Cache directory.</param>
    /// <param name="logger">Logger.</param>
    public WarningCache(string directory, ILogger<WarningCache> logger)
    {
        _directory = directory;
        _matchesPath = Path.Combine(directory, "matches.json");
        _mediaDirectory = Path.Combine(directory, "media");
        _topicsPath = Path.Combine(directory, "topics.json");
        _badgesPath = Path.Combine(directory, "badges.json");
        BadgesDirectory = Path.Combine(directory, "badges");
        _logger = logger;

        Directory.CreateDirectory(_directory);
        Directory.CreateDirectory(_mediaDirectory);
        Directory.CreateDirectory(BadgesDirectory);
        LoadMatches();
        LoadBadges();
    }

    /// <summary>
    /// Gets the directory that holds the badged images.
    /// </summary>
    public string BadgesDirectory { get; }

    /// <summary>
    /// Gets every cached match.
    /// </summary>
    /// <returns>The match entries keyed by Jellyfin item id.</returns>
    public IReadOnlyDictionary<Guid, MatchEntry> GetMatches() => _matches;

    /// <summary>
    /// Gets one cached match.
    /// </summary>
    /// <param name="itemId">Jellyfin item id.</param>
    /// <returns>The entry, or null.</returns>
    public MatchEntry? GetMatch(Guid itemId) => _matches.TryGetValue(itemId, out var entry) ? entry : null;

    /// <summary>
    /// Stores a match entry and persists the match file.
    /// </summary>
    /// <param name="itemId">Jellyfin item id.</param>
    /// <param name="entry">Entry to store.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task.</returns>
    public async Task SaveMatchAsync(Guid itemId, MatchEntry entry, CancellationToken cancellationToken)
    {
        _matches[itemId] = entry;

        await _ioGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await WriteJsonAtomicAsync(_matchesPath, _matches.ToDictionary(pair => pair.Key, pair => pair.Value), cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            _ioGate.Release();
        }
    }

    /// <summary>
    /// Gets the badge applied to one item.
    /// </summary>
    /// <param name="itemId">Jellyfin item id.</param>
    /// <returns>The entry, or null when the item is not badged.</returns>
    public BadgeEntry? GetBadge(Guid itemId) => _badges.TryGetValue(itemId, out var entry) ? entry : null;

    /// <summary>
    /// Gets every badged item.
    /// </summary>
    /// <returns>The badge entries keyed by Jellyfin item id.</returns>
    public IReadOnlyDictionary<Guid, BadgeEntry> GetBadges() => _badges;

    /// <summary>
    /// Stores a badge entry in memory. Call <see cref="SaveBadgesAsync"/> to persist it.
    /// </summary>
    /// <param name="itemId">Jellyfin item id.</param>
    /// <param name="entry">Entry to store.</param>
    public void SetBadge(Guid itemId, BadgeEntry entry) => _badges[itemId] = entry;

    /// <summary>
    /// Forgets a badge entry in memory. Call <see cref="SaveBadgesAsync"/> to persist it.
    /// </summary>
    /// <param name="itemId">Jellyfin item id.</param>
    public void RemoveBadge(Guid itemId) => _badges.TryRemove(itemId, out _);

    /// <summary>
    /// Persists the badge state.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task.</returns>
    public async Task SaveBadgesAsync(CancellationToken cancellationToken)
    {
        await _ioGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await WriteJsonAtomicAsync(_badgesPath, _badges.ToDictionary(pair => pair.Key, pair => pair.Value), cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            _ioGate.Release();
        }
    }

    /// <summary>
    /// Gets a cached media payload.
    /// </summary>
    /// <param name="dtddId">DoesTheDogDie item id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The payload, or null.</returns>
    public async Task<CachedMedia?> GetMediaAsync(int dtddId, CancellationToken cancellationToken)
    {
        if (_media.TryGetValue(dtddId, out var cached))
        {
            return cached;
        }

        var path = MediaPath(dtddId);
        if (!File.Exists(path))
        {
            return null;
        }

        await _ioGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_media.TryGetValue(dtddId, out cached))
            {
                return cached;
            }

            var loaded = await ReadJsonAsync<CachedMedia>(path, cancellationToken).ConfigureAwait(false);
            if (loaded is not null)
            {
                _media[dtddId] = loaded;
            }

            return loaded;
        }
        finally
        {
            _ioGate.Release();
        }
    }

    /// <summary>
    /// Stores a media payload.
    /// </summary>
    /// <param name="media">Payload to store.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task.</returns>
    public async Task SaveMediaAsync(CachedMedia media, CancellationToken cancellationToken)
    {
        _media[media.DtddId] = media;

        await _ioGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await WriteJsonAtomicAsync(MediaPath(media.DtddId), media, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _ioGate.Release();
        }
    }

    /// <summary>
    /// Gets the cached topic taxonomy.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The taxonomy, or null.</returns>
    public async Task<TopicsCache?> GetTopicsAsync(CancellationToken cancellationToken)
    {
        if (_topics is not null)
        {
            return _topics;
        }

        if (!File.Exists(_topicsPath))
        {
            return null;
        }

        await _ioGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _topics ??= await ReadJsonAsync<TopicsCache>(_topicsPath, cancellationToken).ConfigureAwait(false);
            return _topics;
        }
        finally
        {
            _ioGate.Release();
        }
    }

    /// <summary>
    /// Stores the topic taxonomy.
    /// </summary>
    /// <param name="topics">Taxonomy to store.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task.</returns>
    public async Task SaveTopicsAsync(TopicsCache topics, CancellationToken cancellationToken)
    {
        _topics = topics;

        await _ioGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await WriteJsonAtomicAsync(_topicsPath, topics, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _ioGate.Release();
        }
    }

    private string MediaPath(int dtddId) => Path.Combine(_mediaDirectory, $"{dtddId}.json");

    private void LoadMatches()
    {
        if (!File.Exists(_matchesPath))
        {
            return;
        }

        try
        {
            var json = File.ReadAllText(_matchesPath);
            var entries = JsonSerializer.Deserialize<Dictionary<Guid, MatchEntry>>(json);
            if (entries is null)
            {
                return;
            }

            foreach (var (itemId, entry) in entries)
            {
                _matches[itemId] = entry;
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Could not read the content warning match cache at {Path}. Starting empty.", _matchesPath);
        }
    }

    private void LoadBadges()
    {
        if (!File.Exists(_badgesPath))
        {
            return;
        }

        try
        {
            var entries = JsonSerializer.Deserialize<Dictionary<Guid, BadgeEntry>>(File.ReadAllText(_badgesPath));
            if (entries is null)
            {
                return;
            }

            foreach (var (itemId, entry) in entries)
            {
                entry.Images ??= [];
                if (entry.Images.Count == 0 && !string.IsNullOrWhiteSpace(entry.BadgedPath))
                {
                    entry.Images.Add(new BadgedImage
                    {
                        Type = ImageType.Primary,
                        Index = 0,
                        OriginalPath = entry.OriginalPath,
                        BadgedPath = entry.BadgedPath
                    });
                    entry.OriginalPath = string.Empty;
                    entry.BadgedPath = string.Empty;
                }

                _badges[itemId] = entry;
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Could not read the poster badge state at {Path}. Starting empty.", _badgesPath);
        }
    }

    private async Task<T?> ReadJsonAsync<T>(string path, CancellationToken cancellationToken)
        where T : class
    {
        try
        {
            await using var stream = File.OpenRead(path);
            return await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Could not read cache file {Path}.", path);
            return null;
        }
    }

    private static async Task WriteJsonAtomicAsync<T>(string path, T value, CancellationToken cancellationToken)
    {
        var temporary = path + ".tmp";
        await using (var stream = File.Create(temporary))
        {
            await JsonSerializer.SerializeAsync(stream, value, JsonOptions, cancellationToken).ConfigureAwait(false);
        }

        File.Move(temporary, path, true);
    }
}
