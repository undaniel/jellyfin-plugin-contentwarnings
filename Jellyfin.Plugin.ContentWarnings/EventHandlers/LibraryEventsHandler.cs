using System.Collections.Concurrent;
using Jellyfin.Plugin.ContentWarnings.Services;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.ContentWarnings.EventHandlers;

/// <summary>
/// Queues newly added items and restores tags after metadata updates, without feedback loops.
/// </summary>
public sealed class LibraryEventsHandler : IHostedService, IDisposable
{
    private static readonly TimeSpan AddedDebounce = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan UpdatedDebounce = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(10);

    private readonly ILibraryManager _libraryManager;
    private readonly ContentWarningService _service;
    private readonly ILogger<LibraryEventsHandler> _logger;
    private readonly ConcurrentDictionary<Guid, DateTimeOffset> _added = new();
    private readonly ConcurrentDictionary<Guid, DateTimeOffset> _updated = new();
    private CancellationTokenSource? _stopping;
    private Task? _worker;

    /// <summary>
    /// Initializes a new instance of the <see cref="LibraryEventsHandler"/> class.
    /// </summary>
    /// <param name="libraryManager">Jellyfin library manager.</param>
    /// <param name="service">Content warning service.</param>
    /// <param name="logger">Logger.</param>
    public LibraryEventsHandler(
        ILibraryManager libraryManager,
        ContentWarningService service,
        ILogger<LibraryEventsHandler> logger)
    {
        _libraryManager = libraryManager;
        _service = service;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _libraryManager.ItemAdded += OnItemAdded;
        _libraryManager.ItemUpdated += OnItemUpdated;
        _stopping = new CancellationTokenSource();
        _worker = Task.Run(() => ProcessQueueAsync(_stopping.Token), CancellationToken.None);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _libraryManager.ItemAdded -= OnItemAdded;
        _libraryManager.ItemUpdated -= OnItemUpdated;

        if (_stopping is not null)
        {
            await _stopping.CancelAsync().ConfigureAwait(false);
        }

        if (_worker is not null)
        {
            await _worker.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _stopping?.Dispose();
        _worker?.Dispose();
    }

    private void OnItemAdded(object? sender, ItemChangeEventArgs eventArgs)
    {
        if (!Plugin.Instance?.Configuration.ProcessOnItemAdded is true)
        {
            return;
        }

        if (eventArgs.Item is BaseItem item && _service.IsInScope(item))
        {
            _added[item.Id] = DateTimeOffset.UtcNow + AddedDebounce;
        }
    }

    private void OnItemUpdated(object? sender, ItemChangeEventArgs eventArgs)
    {
        if (eventArgs.Item is not BaseItem item
            || !_service.IsInScope(item)
            || _service.IsWriting(item.Id))
        {
            return;
        }

        _updated[item.Id] = DateTimeOffset.UtcNow + UpdatedDebounce;
    }

    private async Task ProcessQueueAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(PollInterval, cancellationToken).ConfigureAwait(false);
                await DrainAsync(_added, added => _service.ProcessItemAsync(added, force: false, cancellationToken), cancellationToken)
                    .ConfigureAwait(false);
                await DrainAsync(_updated, updated => _service.ReapplyItemAsync(updated, cancellationToken), cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Content warnings event queue failed; it will retry on the next poll.");
            }
        }
    }

    private async Task DrainAsync(
        ConcurrentDictionary<Guid, DateTimeOffset> queue,
        Func<Guid, Task> action,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var (itemId, dueAt) in queue)
        {
            if (dueAt > now || !queue.TryRemove(itemId, out _))
            {
                continue;
            }

            try
            {
                await action(itemId).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not process content warnings for queued item {ItemId}.", itemId);
            }
        }
    }
}
