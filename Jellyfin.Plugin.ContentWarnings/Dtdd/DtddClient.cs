using System.Net;
using System.Text.Json;
using Jellyfin.Plugin.ContentWarnings.Configuration;
using Jellyfin.Plugin.ContentWarnings.Dtdd.Dtos;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.ContentWarnings.Dtdd;

/// <summary>
/// HTTP client for the DoesTheDogDie API v3.
/// </summary>
public sealed class DtddClient : IDtddClient
{
    /// <summary>
    /// Named HttpClient used by the plugin.
    /// </summary>
    public const string HttpClientName = "dtdd";

    /// <summary>
    /// Base address of the API.
    /// </summary>
    public static readonly Uri BaseAddress = new("https://www.doesthedogdie.com/api/v3/");

    private const int CircuitBreakerThreshold = 20;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly Func<PluginConfiguration> _configuration;
    private readonly ILogger<DtddClient> _logger;
    private readonly RateLimiter _rateLimiter = new();
    private readonly TimeSpan _retryBaseDelay;
    private readonly int _maxRetries;
    private int _consecutiveErrors;

    /// <summary>
    /// Initializes a new instance of the <see cref="DtddClient"/> class.
    /// </summary>
    /// <param name="httpClient">HTTP client. Its base address must be <see cref="BaseAddress"/>.</param>
    /// <param name="configuration">Supplies the current plugin configuration.</param>
    /// <param name="logger">Logger.</param>
    /// <param name="retryBaseDelay">First retry delay; doubles on each attempt. Defaults to one second.</param>
    /// <param name="maxRetries">Retries after the first attempt. Defaults to 5.</param>
    public DtddClient(
        HttpClient httpClient,
        Func<PluginConfiguration> configuration,
        ILogger<DtddClient> logger,
        TimeSpan? retryBaseDelay = null,
        int? maxRetries = null)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
        _retryBaseDelay = retryBaseDelay ?? TimeSpan.FromSeconds(1);
        _maxRetries = maxRetries ?? 5;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<DtddItem>> SearchByImdbAsync(string imdbId, CancellationToken cancellationToken)
    {
        return SearchAsync("imdb", imdbId, cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<DtddItem>> SearchByTmdbAsync(string tmdbId, CancellationToken cancellationToken)
    {
        return SearchAsync("tmdb", tmdbId, cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<DtddItem>> SearchByTitleAsync(string title, int? year, CancellationToken cancellationToken)
    {
        var query = year.HasValue ? $"{title} {year.Value}" : title;
        return SearchAsync("q", query, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<DtddItemDetail> GetItemAsync(int dtddId, CancellationToken cancellationToken)
    {
        return await GetAsync<DtddItemDetail>($"items/{dtddId}", cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DtddTopic>> GetTopicsAsync(CancellationToken cancellationToken)
    {
        var topics = await GetAsync<List<DtddTopic>>("topics", cancellationToken).ConfigureAwait(false);
        return topics;
    }

    private async Task<IReadOnlyList<DtddItem>> SearchAsync(string parameter, string value, CancellationToken cancellationToken)
    {
        var url = $"items?{parameter}={Uri.EscapeDataString(value)}";
        var items = await GetAsync<List<DtddItem>>(url, cancellationToken).ConfigureAwait(false);
        return items;
    }

    private async Task<T> GetAsync<T>(string relativeUrl, CancellationToken cancellationToken)
    {
        var configuration = _configuration();
        var apiKey = configuration.ApiKey?.Trim();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new DtddAuthException("No DoesTheDogDie API key is configured.");
        }

        for (var attempt = 0; attempt <= _maxRetries; attempt++)
        {
            if (Volatile.Read(ref _consecutiveErrors) >= CircuitBreakerThreshold)
            {
                throw new DtddUnavailableException(
                    $"DoesTheDogDie circuit breaker open after {CircuitBreakerThreshold} consecutive errors.");
            }

            await _rateLimiter.WaitAsync(configuration.RequestsPerSecond, cancellationToken).ConfigureAwait(false);

            using var request = new HttpRequestMessage(HttpMethod.Get, relativeUrl);
            request.Headers.TryAddWithoutValidation("X-API-KEY", apiKey);
            request.Headers.TryAddWithoutValidation("Accept", "application/json");

            try
            {
                using var response = await _httpClient
                    .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                    .ConfigureAwait(false);

                if (response.IsSuccessStatusCode)
                {
                    Interlocked.Exchange(ref _consecutiveErrors, 0);
                    var value = await JsonSerializer
                        .DeserializeAsync<T>(
                            await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false),
                            JsonOptions,
                            cancellationToken)
                        .ConfigureAwait(false);

                    return value ?? throw new DtddException("DoesTheDogDie returned an empty JSON document.");
                }

                if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                {
                    throw new DtddAuthException(
                        $"DoesTheDogDie rejected the API key ({(int)response.StatusCode}). "
                        + "Check the key in the plugin configuration; a Cloudflare block also returns 403.");
                }

                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    throw new DtddNotFoundException($"DoesTheDogDie returned 404 for '{relativeUrl}'.");
                }

                var retryable = response.StatusCode == HttpStatusCode.TooManyRequests
                    || (int)response.StatusCode >= 500;

                if (!retryable)
                {
                    throw new DtddException(
                        $"DoesTheDogDie returned unexpected status {(int)response.StatusCode} for '{relativeUrl}'.");
                }

                Interlocked.Increment(ref _consecutiveErrors);
                if (attempt == _maxRetries)
                {
                    break;
                }

                var delay = GetDelay(response, attempt);
                _logger.LogWarning(
                    "DoesTheDogDie returned {Status}; retrying in {DelaySeconds:0.0}s (attempt {Attempt}/{MaxRetries}).",
                    (int)response.StatusCode,
                    delay.TotalSeconds,
                    attempt + 1,
                    _maxRetries);
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }
            catch (HttpRequestException ex)
            {
                Interlocked.Increment(ref _consecutiveErrors);
                if (attempt == _maxRetries)
                {
                    _logger.LogWarning(ex, "DoesTheDogDie request failed on the last attempt.");
                    break;
                }

                var delay = GetDelay(null, attempt);
                _logger.LogWarning(
                    ex,
                    "DoesTheDogDie request failed; retrying in {DelaySeconds:0.0}s (attempt {Attempt}/{MaxRetries}).",
                    delay.TotalSeconds,
                    attempt + 1,
                    _maxRetries);
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }
            catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                Interlocked.Increment(ref _consecutiveErrors);
                if (attempt == _maxRetries)
                {
                    _logger.LogWarning(ex, "DoesTheDogDie request timed out on the last attempt.");
                    break;
                }

                var delay = GetDelay(null, attempt);
                _logger.LogWarning(
                    ex,
                    "DoesTheDogDie request timed out; retrying in {DelaySeconds:0.0}s (attempt {Attempt}/{MaxRetries}).",
                    delay.TotalSeconds,
                    attempt + 1,
                    _maxRetries);
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }
        }

        throw new DtddUnavailableException(
            $"DoesTheDogDie did not answer after {_maxRetries + 1} attempts for '{relativeUrl}'.");
    }

    private TimeSpan GetDelay(HttpResponseMessage? response, int attempt)
    {
        if (response?.Headers.RetryAfter is { } retryAfter)
        {
            if (retryAfter.Delta is { } delta)
            {
                return Clamp(delta);
            }

            if (retryAfter.Date is { } date)
            {
                return Clamp(date - DateTimeOffset.UtcNow);
            }
        }

        var seconds = _retryBaseDelay.TotalSeconds * Math.Pow(2, attempt);
        return Clamp(TimeSpan.FromSeconds(seconds));
    }

    private static TimeSpan Clamp(TimeSpan delay)
    {
        if (delay < TimeSpan.Zero)
        {
            return TimeSpan.Zero;
        }

        return delay > TimeSpan.FromSeconds(60) ? TimeSpan.FromSeconds(60) : delay;
    }
}
