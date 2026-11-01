using Jellyfin.Data.Enums;
using Jellyfin.Plugin.ContentWarnings.Configuration;
using Jellyfin.Plugin.ContentWarnings.Core;
using Jellyfin.Plugin.ContentWarnings.Imaging;
using Jellyfin.Plugin.ContentWarnings.Storage;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.ContentWarnings.Services;

/// <summary>
/// Bakes a warning badge into the Primary image of items that carry content warning tags.
/// The badged image replaces the original as the item image, so every client shows it.
/// </summary>
public sealed class PosterBadgeService
{
    private readonly ILibraryManager _libraryManager;
    private readonly WarningCache _cache;
    private readonly ILogger<PosterBadgeService> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>
    /// Initializes a new instance of the <see cref="PosterBadgeService"/> class.
    /// </summary>
    /// <param name="libraryManager">Jellyfin library manager.</param>
    /// <param name="cache">Plugin cache.</param>
    /// <param name="logger">Logger.</param>
    public PosterBadgeService(
        ILibraryManager libraryManager,
        WarningCache cache,
        ILogger<PosterBadgeService> logger)
    {
        _libraryManager = libraryManager;
        _cache = cache;
        _logger = logger;
    }

    /// <summary>
    /// Adds or removes the badge for one item. Episodes inherit the badges of their series.
    /// </summary>
    /// <param name="item">Item to sync.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task.</returns>
    public async Task SyncAsync(BaseItem item, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (await SyncCoreAsync(item, cancellationToken).ConfigureAwait(false))
            {
                await _cache.SaveBadgesAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Could not update the poster badge of {ItemName} ({ItemId}).", item.Name, item.Id);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Adds or removes the badge for every episode of a series.
    /// </summary>
    /// <param name="series">Series whose episodes inherit its tags.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task.</returns>
    public async Task SyncSeriesEpisodesAsync(Series series, CancellationToken cancellationToken)
    {
        var episodes = _libraryManager.GetItemList(new InternalItemsQuery
        {
            ParentId = series.Id,
            Recursive = true,
            IncludeItemTypes = [BaseItemKind.Episode]
        });

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var changed = false;
            foreach (var episode in episodes)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    changed |= await SyncCoreAsync(episode, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogWarning(ex, "Could not update the poster badge of {ItemName} ({ItemId}).", episode.Name, episode.Id);
                }
            }

            if (changed)
            {
                await _cache.SaveBadgesAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Deletes badged images that belong to items that no longer exist.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task.</returns>
    public async Task CleanupAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await CleanupCoreAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task CleanupCoreAsync(CancellationToken cancellationToken)
    {
        var removed = 0;

        foreach (var file in Directory.EnumerateFiles(_cache.BadgesDirectory))
        {
            var name = Path.GetFileName(file);
            var separator = name.IndexOf('-');
            if (separator <= 0
                || !Guid.TryParse(name[..separator], out var itemId)
                || _libraryManager.GetItemById(itemId) is not null)
            {
                continue;
            }

            DeleteFiles(itemId);
            _cache.RemoveBadge(itemId);
            removed++;
        }

        foreach (var itemId in _cache.GetBadges().Keys.ToList())
        {
            if (_libraryManager.GetItemById(itemId) is null)
            {
                DeleteFiles(itemId);
                _cache.RemoveBadge(itemId);
                removed++;
            }
        }

        if (removed > 0)
        {
            await _cache.SaveBadgesAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<bool> SyncCoreAsync(BaseItem item, CancellationToken cancellationToken)
    {
        var configuration = Plugin.Instance?.Configuration ?? new PluginConfiguration();
        var entry = _cache.GetBadge(item.Id);
        var tags = EffectiveTags(item);
        var wanted = BadgeDecision.ShouldBadge(configuration.ShowPosterBadge, configuration.TagPrefix, tags);

        if (!wanted)
        {
            if (entry is null)
            {
                return false;
            }

            await RestoreAsync(item, entry, cancellationToken).ConfigureAwait(false);
            return true;
        }

        var current = item.GetImageInfo(ImageType.Primary, 0);
        if (current is null)
        {
            return false;
        }

        var hash = BadgeDecision.Fingerprint(configuration.TagPrefix!, tags!);
        var ours = entry is not null && string.Equals(current.Path, entry.BadgedPath, StringComparison.OrdinalIgnoreCase);
        if (ours && entry!.Hash == hash && File.Exists(entry.BadgedPath))
        {
            return false;
        }

        if (entry is null && current.Path.StartsWith(_cache.BadgesDirectory, StringComparison.OrdinalIgnoreCase))
        {
            // shortcut: with no state we cannot tell whether the file is already badged, so it stays; upgrade to a per-item sidecar to recover the original.
            return false;
        }

        var originalPath = ours ? entry!.OriginalPath : current.Path;
        var source = await ResolveLocalImageAsync(item, originalPath, cancellationToken).ConfigureAwait(false);
        if (source is null)
        {
            _logger.LogDebug("No readable image for {ItemName}; skipping the badge.", item.Name);
            return false;
        }

        string badgedPath;
        try
        {
            badgedPath = BadgeImage.Render(source, _cache.BadgesDirectory, item.Id);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Could not render the poster badge for {ItemName}.", item.Name);
            return false;
        }

        item.SetImage(
            new ItemImageInfo
            {
                Path = badgedPath,
                Type = ImageType.Primary,
                DateModified = File.GetLastWriteTimeUtc(badgedPath)
            },
            0);
        await item.UpdateToRepositoryAsync(ItemUpdateType.ImageUpdate, cancellationToken).ConfigureAwait(false);
        DeleteFiles(item.Id, badgedPath);

        _cache.SetBadge(item.Id, new BadgeEntry
        {
            Hash = hash,
            OriginalPath = originalPath,
            BadgedPath = badgedPath
        });
        return true;
    }

    private async Task RestoreAsync(BaseItem item, BadgeEntry entry, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(entry.OriginalPath))
        {
            item.SetImage(
                new ItemImageInfo
                {
                    Path = entry.OriginalPath,
                    Type = ImageType.Primary,
                    DateModified = File.Exists(entry.OriginalPath)
                        ? File.GetLastWriteTimeUtc(entry.OriginalPath)
                        : DateTime.MinValue
                },
                0);
            await item.UpdateToRepositoryAsync(ItemUpdateType.ImageUpdate, cancellationToken).ConfigureAwait(false);
        }

        DeleteFiles(item.Id);
        _cache.RemoveBadge(item.Id);
    }

    private IReadOnlyCollection<string>? EffectiveTags(BaseItem item)
    {
        if (item is not Episode episode)
        {
            return item.Tags;
        }

        var seriesId = episode.SeriesId == Guid.Empty ? episode.FindSeriesId() : episode.SeriesId;
        return seriesId == Guid.Empty ? null : _libraryManager.GetItemById(seriesId)?.Tags;
    }

    private async Task<string?> ResolveLocalImageAsync(BaseItem item, string path, CancellationToken cancellationToken)
    {
        if (File.Exists(path))
        {
            return path;
        }

        if (!Uri.TryCreate(path, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return null;
        }

        try
        {
            var local = await _libraryManager
                .ConvertImageToLocal(item, new ItemImageInfo { Path = path, Type = ImageType.Primary }, 0, removeOnFailure: false)
                .ConfigureAwait(false);
            return File.Exists(local.Path) ? local.Path : null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogDebug(ex, "Could not download the image of {ItemName}.", item.Name);
            return null;
        }
    }

    private void DeleteFiles(Guid itemId, string? keep = null)
    {
        foreach (var file in Directory.EnumerateFiles(_cache.BadgesDirectory, $"{itemId}-*"))
        {
            if (keep is not null && string.Equals(file, keep, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            try
            {
                File.Delete(file);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logger.LogDebug(ex, "Could not delete the badged image {Path}.", file);
            }
        }
    }
}
