using Jellyfin.Plugin.ContentWarnings.Services;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.ContentWarnings.Tasks;

/// <summary>
/// Fast task that restores tags from the local cache without network calls.
/// </summary>
public sealed class ReapplyTagsTask : IScheduledTask
{
    private readonly ContentWarningService _service;
    private readonly ILogger<ReapplyTagsTask> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReapplyTagsTask"/> class.
    /// </summary>
    /// <param name="service">Content warning service.</param>
    /// <param name="logger">Logger.</param>
    public ReapplyTagsTask(ContentWarningService service, ILogger<ReapplyTagsTask> logger)
    {
        _service = service;
        _logger = logger;
    }

    /// <inheritdoc />
    public string Name => "Reapply content warning tags";

    /// <inheritdoc />
    public string Key => "ContentWarningsReapply";

    /// <inheritdoc />
    public string Description => "Restores content warning tags from the local cache, for example after a metadata refresh.";

    /// <inheritdoc />
    public string Category => "Content Warnings";

    /// <inheritdoc />
    public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        var count = await _service.ReapplyAllAsync(progress, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Content warning tags reapplied after visiting {Count} items.", count);
    }

    /// <inheritdoc />
    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
    {
        return
        [
            new TaskTriggerInfo
            {
                Type = TaskTriggerInfoType.DailyTrigger,
                TimeOfDayTicks = TimeSpan.FromHours(4).Ticks
            }
        ];
    }
}
