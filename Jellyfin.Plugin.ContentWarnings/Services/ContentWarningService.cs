using System.Collections.Concurrent;
using Jellyfin.Data.Enums;
using Jellyfin.Plugin.ContentWarnings.Api;
using Jellyfin.Plugin.ContentWarnings.Configuration;
using Jellyfin.Plugin.ContentWarnings.Core;
using Jellyfin.Plugin.ContentWarnings.Dtdd;
using Jellyfin.Plugin.ContentWarnings.Dtdd.Dtos;
using Jellyfin.Plugin.ContentWarnings.Storage;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.ContentWarnings.Services;

/// <summary>
/// Orchestrates matching, fetching, evaluation and tag application.
/// </summary>
public sealed class ContentWarningService
{
    /// <summary>
    /// Required attribution phrase.
    /// </summary>
    public const string Attribution = "Powered by DoesTheDogDie.com";

    private readonly IDtddClient _client;
    private readonly WarningCache _cache;
    private readonly ILibraryManager _libraryManager;
    private readonly PosterBadgeService _badges;
    private readonly ILogger<ContentWarningService> _logger;
    private readonly ConcurrentDictionary<Guid, DateTimeOffset> _writing = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="ContentWarningService"/> class.
    /// </summary>
    /// <param name="client">DoesTheDogDie client.</param>
    /// <param name="cache">File cache.</param>
    /// <param name="libraryManager">Jellyfin library manager.</param>
    /// <param name="badges">Poster badge service.</param>
    /// <param name="logger">Logger.</param>
    public ContentWarningService(
        IDtddClient client,
        WarningCache cache,
        ILibraryManager libraryManager,
        PosterBadgeService badges,
        ILogger<ContentWarningService> logger)
    {
        _client = client;
        _cache = cache;
        _libraryManager = libraryManager;
        _badges = badges;
        _logger = logger;
    }

    private static PluginConfiguration Configuration => Plugin.Instance?.Configuration ?? new PluginConfiguration();

    /// <summary>
    /// Gets whether the plugin is currently writing tags to an item.
    /// </summary>
    /// <param name="itemId">Jellyfin item id.</param>
    /// <returns>True while the item is inside the write window.</returns>
    public bool IsWriting(Guid itemId)
    {
        if (!_writing.TryGetValue(itemId, out var expiresAt))
        {
            return false;
        }

        if (expiresAt > DateTimeOffset.UtcNow)
        {
            return true;
        }

        _writing.TryRemove(itemId, out _);
        return false;
    }

    /// <summary>
    /// Gets whether an item is a movie or series inside the configured libraries.
    /// </summary>
    /// <param name="item">Jellyfin item.</param>
    /// <returns>True when the plugin should process it.</returns>
    public bool IsInScope(BaseItem item)
    {
        if (item is not Movie && item is not Series)
        {
            return false;
        }

        var configuration = Configuration;
        if (configuration.LibraryIds.Count == 0)
        {
            return true;
        }

        return _libraryManager.GetCollectionFolders(item)
            .Any(folder => configuration.LibraryIds.Contains(folder.Id));
    }

    /// <summary>
    /// Gets whether an item is outside the tag scope but still inherits another item's badge.
    /// </summary>
    /// <param name="item">Jellyfin item.</param>
    /// <returns>True when the item is an episode of a series inside the configured libraries.</returns>
    public bool IsBadgeable(BaseItem item)
    {
        if (item is not Episode episode)
        {
            return false;
        }

        var seriesId = episode.SeriesId == Guid.Empty ? episode.FindSeriesId() : episode.SeriesId;
        return seriesId != Guid.Empty
               && _libraryManager.GetItemById(seriesId) is { } series
               && IsInScope(series);
    }

    /// <summary>
    /// Processes every movie and series in scope.
    /// </summary>
    /// <param name="progress">Progress reporter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of items visited.</returns>
    public async Task<int> UpdateAllAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        var items = GetItemsInScope();
        var index = 0;

        foreach (var item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            progress.Report(items.Count == 0 ? 100 : 100.0 * index / items.Count);

            try
            {
                await ProcessItemAsync(item, force: false, cancellationToken).ConfigureAwait(false);
            }
            catch (DtddAuthException)
            {
                throw;
            }
            catch (DtddUnavailableException)
            {
                throw;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Failed to process content warnings for {ItemName} ({ItemId}).", item.Name, item.Id);
            }

            index++;
        }

        progress.Report(100);
        return index;
    }

    /// <summary>
    /// Reapplies tags from the local cache, without network calls.
    /// </summary>
    /// <param name="progress">Progress reporter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of items visited.</returns>
    public async Task<int> ReapplyAllAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        await _badges.CleanupAsync(cancellationToken).ConfigureAwait(false);
        var items = GetItemsInScope();
        var index = 0;

        foreach (var item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            progress.Report(items.Count == 0 ? 100 : 100.0 * index / items.Count);

            try
            {
                await ReapplyItemAsync(item, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Failed to reapply content warning tags to {ItemName} ({ItemId}).", item.Name, item.Id);
            }

            index++;
        }

        progress.Report(100);
        return index;
    }

    /// <summary>
    /// Processes one item by id.
    /// </summary>
    /// <param name="itemId">Jellyfin item id.</param>
    /// <param name="force">True to ignore the no-match retry delay and media TTL.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task.</returns>
    public async Task ProcessItemAsync(Guid itemId, bool force, CancellationToken cancellationToken)
    {
        var item = _libraryManager.GetItemById(itemId);
        if (item is null)
        {
            throw new DtddNotFoundException($"Jellyfin item {itemId} was not found.");
        }

        await ProcessItemAsync(item, force, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Reapplies tags for one item using only the cache.
    /// </summary>
    /// <param name="itemId">Jellyfin item id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task.</returns>
    public async Task ReapplyItemAsync(Guid itemId, CancellationToken cancellationToken)
    {
        var item = _libraryManager.GetItemById(itemId);
        if (item is null)
        {
            return;
        }

        await ReapplyItemAsync(item, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Builds the API response for one item from the cache.
    /// </summary>
    /// <param name="itemId">Jellyfin item id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The response.</returns>
    public async Task<ItemWarningsResponse> GetItemWarningsAsync(Guid itemId, CancellationToken cancellationToken)
    {
        var match = _cache.GetMatch(itemId);
        if (match?.DtddId is not { } dtddId)
        {
            return new ItemWarningsResponse(
                itemId,
                null,
                null,
                match?.Method ?? MatchMethods.NoMatch,
                null,
                [],
                Attribution);
        }

        var media = await _cache.GetMediaAsync(dtddId, cancellationToken).ConfigureAwait(false);
        var warnings = Evaluate(media, Configuration).Select(warning => new WarningResponse(
            warning.TopicId,
            warning.Label,
            warning.Yes,
            warning.No,
            warning.Ratio,
            warning.IsSpoiler)).ToList();

        return new ItemWarningsResponse(
            itemId,
            dtddId,
            $"https://www.doesthedogdie.com/media/{dtddId}",
            match.Method,
            media?.FetchedAt,
            warnings,
            Attribution);
    }

    /// <summary>
    /// Stores a manual match. It is never replaced by the automatic matcher.
    /// </summary>
    /// <param name="itemId">Jellyfin item id.</param>
    /// <param name="dtddId">DoesTheDogDie id, or null for "no match, do not retry".</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task.</returns>
    public async Task SetManualMatchAsync(Guid itemId, int? dtddId, CancellationToken cancellationToken)
    {
        await _cache.SaveMatchAsync(
            itemId,
            new MatchEntry
            {
                DtddId = dtddId,
                Method = MatchMethods.Manual,
                CheckedAt = DateTimeOffset.UtcNow
            },
            cancellationToken).ConfigureAwait(false);

        if (dtddId is null && _libraryManager.GetItemById(itemId) is { } item)
        {
            await ApplyTagsAsync(item, null, Configuration, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Gets items that have no match.
    /// </summary>
    /// <returns>The unmatched items that still exist in the library.</returns>
    public IReadOnlyList<UnmatchedItemResponse> GetUnmatchedItems()
    {
        var result = new List<UnmatchedItemResponse>();

        foreach (var (itemId, entry) in _cache.GetMatches())
        {
            if (entry.DtddId.HasValue || entry.Method == MatchMethods.Manual)
            {
                continue;
            }

            var item = _libraryManager.GetItemById(itemId);
            if (item is null || !IsInScope(item))
            {
                continue;
            }

            result.Add(new UnmatchedItemResponse(
                item.Id,
                item.Name,
                item.ProductionYear,
                item is Movie ? "Movie" : "Series",
                entry.CheckedAt));
        }

        return result.OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>
    /// Gets the topic taxonomy, from cache when fresh.
    /// </summary>
    /// <param name="force">True to bypass the cache.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The topics.</returns>
    public async Task<IReadOnlyList<CachedTopicInfo>> GetTopicsAsync(bool force, CancellationToken cancellationToken)
    {
        var configuration = Configuration;
        var cached = await _cache.GetTopicsAsync(cancellationToken).ConfigureAwait(false);
        var maxAge = TimeSpan.FromDays(Math.Max(1, configuration.CacheTtlDays));
        var stale = cached is null || DateTimeOffset.UtcNow - cached.FetchedAt > maxAge;

        if (!force && !stale)
        {
            return cached!.Topics;
        }

        try
        {
            var topics = await _client.GetTopicsAsync(cancellationToken).ConfigureAwait(false);
            var fresh = new TopicsCache
            {
                FetchedAt = DateTimeOffset.UtcNow,
                Topics = topics
                    .Select(topic => new CachedTopicInfo
                    {
                        Id = topic.Id,
                        Name = topic.Name,
                        DoesName = topic.DoesName,
                        Keywords = topic.Keywords,
                        TopicCategoryId = topic.TopicCategoryId
                    })
                    .ToList()
            };

            await _cache.SaveTopicsAsync(fresh, cancellationToken).ConfigureAwait(false);
            return fresh.Topics;
        }
        catch (DtddException ex) when (cached is not null)
        {
            _logger.LogWarning(ex, "Could not refresh the DoesTheDogDie topic list; using the cached copy.");
            return cached.Topics;
        }
    }

    /// <summary>
    /// Checks that the configured API key works.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result.</returns>
    public async Task<TestConnectionResponse> TestConnectionAsync(CancellationToken cancellationToken)
    {
        try
        {
            var topics = await _client.GetTopicsAsync(cancellationToken).ConfigureAwait(false);
            return new TestConnectionResponse(true, $"Connection OK. {topics.Count} topics available.");
        }
        catch (DtddAuthException ex)
        {
            return new TestConnectionResponse(false, ex.Message);
        }
        catch (DtddException ex)
        {
            return new TestConnectionResponse(false, ex.Message);
        }
        catch (HttpRequestException ex)
        {
            return new TestConnectionResponse(false, $"Network error: {ex.Message}");
        }
    }

    private async Task ProcessItemAsync(BaseItem item, bool force, CancellationToken cancellationToken)
    {
        if (item is Episode)
        {
            await _badges.SyncAsync(item, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (!IsInScope(item))
        {
            return;
        }

        var configuration = Configuration;
        var match = await ResolveMatchAsync(item, configuration, force, cancellationToken).ConfigureAwait(false);
        if (match?.DtddId is not { } dtddId)
        {
            return;
        }

        var media = await GetMediaAsync(dtddId, configuration, force, cancellationToken).ConfigureAwait(false);
        await ApplyTagsAsync(item, media, configuration, cancellationToken).ConfigureAwait(false);
    }

    private async Task ReapplyItemAsync(BaseItem item, CancellationToken cancellationToken)
    {
        if (item is Episode)
        {
            await _badges.SyncAsync(item, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (!IsInScope(item))
        {
            return;
        }

        var match = _cache.GetMatch(item.Id);
        if (match?.DtddId is not { } dtddId)
        {
            await SyncBadgesAsync(item, cancellationToken).ConfigureAwait(false);
            return;
        }

        var media = await _cache.GetMediaAsync(dtddId, cancellationToken).ConfigureAwait(false);
        await ApplyTagsAsync(item, media, Configuration, cancellationToken).ConfigureAwait(false);
    }

    private async Task SyncBadgesAsync(BaseItem item, CancellationToken cancellationToken)
    {
        await _badges.SyncAsync(item, cancellationToken).ConfigureAwait(false);
        if (item is Series series)
        {
            await _badges.SyncSeriesEpisodesAsync(series, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<MatchEntry> ResolveMatchAsync(
        BaseItem item,
        PluginConfiguration configuration,
        bool force,
        CancellationToken cancellationToken)
    {
        var existing = _cache.GetMatch(item.Id);
        if (existing is not null)
        {
            if (existing.Method == MatchMethods.Manual)
            {
                return existing;
            }

            if (!force)
            {
                var retryAfter = TimeSpan.FromDays(Math.Max(1, configuration.NotFoundRetryDays));
                if (existing.DtddId.HasValue || DateTimeOffset.UtcNow - existing.CheckedAt < retryAfter)
                {
                    return existing;
                }
            }
        }

        var imdbId = item.GetProviderId(MetadataProvider.Imdb);
        if (!string.IsNullOrWhiteSpace(imdbId))
        {
            var results = await _client.SearchByImdbAsync(imdbId, cancellationToken).ConfigureAwait(false);
            var hit = results.FirstOrDefault(candidate =>
                          string.Equals(candidate.ImdbId, imdbId, StringComparison.OrdinalIgnoreCase))
                      ?? results.FirstOrDefault();

            if (hit is not null)
            {
                return await SaveMatchAsync(item.Id, hit.Id, MatchMethods.Imdb, cancellationToken).ConfigureAwait(false);
            }
        }

        var tmdbId = item.GetProviderId(MetadataProvider.Tmdb);
        if (!string.IsNullOrWhiteSpace(tmdbId))
        {
            var results = await _client.SearchByTmdbAsync(tmdbId, cancellationToken).ConfigureAwait(false);
            var hit = results.FirstOrDefault(candidate =>
                candidate.TmdbId.HasValue && candidate.TmdbId.Value.ToString() == tmdbId);

            if (hit is not null)
            {
                return await SaveMatchAsync(item.Id, hit.Id, MatchMethods.Tmdb, cancellationToken).ConfigureAwait(false);
            }
        }

        var candidates = new Dictionary<int, MatchCandidate>();
        foreach (var title in new[] { item.Name, item.OriginalTitle }
                     .Where(title => !string.IsNullOrWhiteSpace(title))
                     .Distinct(StringComparer.Ordinal))
        {
            var results = await _client
                .SearchByTitleAsync(title, item.ProductionYear, cancellationToken)
                .ConfigureAwait(false);

            foreach (var result in results)
            {
                candidates[result.Id] = new MatchCandidate(
                    result.Id,
                    result.Name,
                    result.ReleaseYear,
                    string.Equals(result.ItemTypeName, "Movie", StringComparison.OrdinalIgnoreCase));
            }
        }

        var decision = MatchScorer.SelectBest(item.Name, item.ProductionYear, item is Movie, candidates.Values);
        if (decision.DtddId is { } matchedId)
        {
            return await SaveMatchAsync(item.Id, matchedId, MatchMethods.Title, cancellationToken).ConfigureAwait(false);
        }

        _logger.LogInformation(
            "No safe DoesTheDogDie match for {ItemName} ({Year}); best score {Score}, runner-up {RunnerUp}.",
            item.Name,
            item.ProductionYear,
            decision.Score,
            decision.RunnerUpScore);

        return await SaveMatchAsync(item.Id, null, MatchMethods.NoMatch, cancellationToken).ConfigureAwait(false);
    }

    private async Task<MatchEntry> SaveMatchAsync(
        Guid itemId,
        int? dtddId,
        string method,
        CancellationToken cancellationToken)
    {
        var entry = new MatchEntry
        {
            DtddId = dtddId,
            Method = method,
            CheckedAt = DateTimeOffset.UtcNow
        };

        await _cache.SaveMatchAsync(itemId, entry, cancellationToken).ConfigureAwait(false);
        return entry;
    }

    private async Task<CachedMedia?> GetMediaAsync(
        int dtddId,
        PluginConfiguration configuration,
        bool force,
        CancellationToken cancellationToken)
    {
        var cached = await _cache.GetMediaAsync(dtddId, cancellationToken).ConfigureAwait(false);
        var maxAge = TimeSpan.FromDays(Math.Max(1, configuration.CacheTtlDays));
        if (!force && cached is not null && DateTimeOffset.UtcNow - cached.FetchedAt <= maxAge)
        {
            return cached;
        }

        try
        {
            var detail = await _client.GetItemAsync(dtddId, cancellationToken).ConfigureAwait(false);
            var media = new CachedMedia
            {
                DtddId = dtddId,
                FetchedAt = DateTimeOffset.UtcNow,
                Topics = detail.TopicItemStats
                    .Select(stat => new CachedTopicVote
                    {
                        Id = stat.TopicId,
                        Name = stat.TopicName,
                        YesSum = stat.YesSum,
                        NoSum = stat.NoSum
                    })
                    .ToList()
            };

            await _cache.SaveMediaAsync(media, cancellationToken).ConfigureAwait(false);
            return media;
        }
        catch (DtddAuthException)
        {
            throw;
        }
        catch (DtddNotFoundException)
        {
            var empty = new CachedMedia { DtddId = dtddId, FetchedAt = DateTimeOffset.UtcNow };
            await _cache.SaveMediaAsync(empty, cancellationToken).ConfigureAwait(false);
            return empty;
        }
        catch (DtddUnavailableException)
        {
            throw;
        }
        catch (DtddException ex)
        {
            if (cached is null)
            {
                _logger.LogWarning(ex, "Could not fetch DoesTheDogDie item {DtddId}; skipping its tags.", dtddId);
                return null;
            }

            _logger.LogWarning(ex, "Could not refresh DoesTheDogDie item {DtddId}; keeping the cached copy.", dtddId);
            return cached;
        }
    }

    private async Task ApplyTagsAsync(
        BaseItem item,
        CachedMedia? media,
        PluginConfiguration configuration,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(configuration.TagPrefix))
        {
            _logger.LogWarning("Content warning tag prefix is empty; refusing to touch tags on {ItemName}.", item.Name);
            await SyncBadgesAsync(item, cancellationToken).ConfigureAwait(false);
            return;
        }

        var desired = Evaluate(media, configuration)
            .Select(warning => warning.Tag)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var merged = TagCalculator.Merge(item.Tags, desired, configuration.TagPrefix);
        if (!TagCalculator.AreEqual(item.Tags, merged))
        {
            _writing[item.Id] = DateTimeOffset.UtcNow.AddSeconds(30);
            item.Tags = merged;
            await item.UpdateToRepositoryAsync(ItemUpdateType.MetadataEdit, cancellationToken).ConfigureAwait(false);
            _logger.LogDebug("Applied {Count} content warning tags to {ItemName}.", desired.Length, item.Name);
        }

        await SyncBadgesAsync(item, cancellationToken).ConfigureAwait(false);
    }

    private static IReadOnlyList<ContentWarning> Evaluate(CachedMedia? media, PluginConfiguration configuration)
    {
        if (media is null)
        {
            return [];
        }

        var options = new WarningOptions(
            configuration.MinYesVotes,
            configuration.MinYesRatio,
            configuration.EnabledTopicIds,
            configuration.TopicLabelOverrides
                .Where(entry => !string.IsNullOrWhiteSpace(entry.Label))
                .GroupBy(entry => entry.TopicId)
                .ToDictionary(group => group.Key, group => group.Last().Label),
            configuration.IncludeSpoilerTopics,
            configuration.TagPrefix);

        return WarningEvaluator.Evaluate(
            media.Topics.Select(topic => new TopicVote(
                topic.Id,
                topic.Name,
                topic.YesSum,
                topic.NoSum,
                topic.DoesName,
                topic.IsSpoiler)),
            options);
    }

    private List<BaseItem> GetItemsInScope()
    {
        var query = new InternalItemsQuery
        {
            IncludeItemTypes = [BaseItemKind.Movie, BaseItemKind.Series],
            Recursive = true,
            IsMissing = false,
            IsUnaired = false
        };

        return _libraryManager.GetItemList(query).Where(IsInScope).ToList();
    }
}
