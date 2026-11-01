namespace Jellyfin.Plugin.ContentWarnings.Dtdd;

/// <summary>
/// Spaces requests to DoesTheDogDie.
/// </summary>
public sealed class RateLimiter
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private DateTime _nextSlot = DateTime.MinValue;

    /// <summary>
    /// Waits until the next request is allowed.
    /// </summary>
    /// <param name="requestsPerSecond">Configured rate. Values below 0.01 are treated as 0.01.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when the request may be sent.</returns>
    public async Task WaitAsync(double requestsPerSecond, CancellationToken cancellationToken)
    {
        var rate = double.IsNaN(requestsPerSecond) || requestsPerSecond < 0.01 ? 0.01 : requestsPerSecond;
        var interval = TimeSpan.FromSeconds(1.0 / rate);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var now = DateTime.UtcNow;
            if (_nextSlot > now)
            {
                await Task.Delay(_nextSlot - now, cancellationToken).ConfigureAwait(false);
            }

            _nextSlot = DateTime.UtcNow + interval;
        }
        finally
        {
            _gate.Release();
        }
    }
}
