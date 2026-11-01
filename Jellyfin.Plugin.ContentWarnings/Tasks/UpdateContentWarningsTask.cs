using Jellyfin.Plugin.ContentWarnings.Dtdd;
using Jellyfin.Plugin.ContentWarnings.Services;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.ContentWarnings.Tasks;

/// <summary>
/// Scheduled task that matches and refreshes every item in scope.
/// </summary>
public sealed class UpdateContentWarningsTask : IScheduledTask
{
    private readonly ContentWarningService _service;
    private readonly ILogger<UpdateContentWarningsTask> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateContentWarningsTask"/> class.
    /// </summary>
    /// <param name="service">Content warning service.</param>
    /// <param name="logger">Logger.</param>
    public UpdateContentWarningsTask(ContentWarningService service, ILogger<UpdateContentWarningsTask> logger)
    {
        _service = service;
        _logger = logger;
    }

    /// <inheritdoc />
    public string Name => "Update content warnings";

    /// <inheritdoc />
    public string Key => "ContentWarningsUpdate";

    /// <inheritdoc />
    public string Description => "Matches movies and series with DoesTheDogDie.com and applies content warning tags.";

    /// <inheritdoc />
    public string Category => "Content Warnings";

    /// <inheritdoc />
    public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        try
        {
            var count = await _service.UpdateAllAsync(progress, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Content warnings task finished after visiting {Count} items.", count);
        }
        catch (DtddAuthException ex)
        {
            _logger.LogError(ex, "Content warnings task aborted: the DoesTheDogDie API key is not valid.");
        }
        catch (DtddUnavailableException ex)
        {
            _logger.LogWarning(ex, "Content warnings task stopped because DoesTheDogDie is unavailable.");
        }
    }

    /// <inheritdoc />
    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
    {
        return
        [
            new TaskTriggerInfo
            {
                Type = TaskTriggerInfoType.DailyTrigger,
                TimeOfDayTicks = TimeSpan.FromHours(3).Ticks
            }
        ];
    }
}
