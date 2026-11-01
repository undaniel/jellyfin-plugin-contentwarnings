using Jellyfin.Plugin.ContentWarnings.Dtdd.Dtos;

namespace Jellyfin.Plugin.ContentWarnings.Dtdd;

/// <summary>
/// DoesTheDogDie API v3 client.
/// </summary>
public interface IDtddClient
{
    /// <summary>
    /// Searches by IMDb id.
    /// </summary>
    /// <param name="imdbId">IMDb id, for example tt2911666.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Matching items.</returns>
    Task<IReadOnlyList<DtddItem>> SearchByImdbAsync(string imdbId, CancellationToken cancellationToken);

    /// <summary>
    /// Searches by TMDb id.
    /// </summary>
    /// <param name="tmdbId">TMDb id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Matching items.</returns>
    Task<IReadOnlyList<DtddItem>> SearchByTmdbAsync(string tmdbId, CancellationToken cancellationToken);

    /// <summary>
    /// Searches by title, optionally with a year hint.
    /// </summary>
    /// <param name="title">Title.</param>
    /// <param name="year">Release year, if known.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Matching items.</returns>
    Task<IReadOnlyList<DtddItem>> SearchByTitleAsync(string title, int? year, CancellationToken cancellationToken);

    /// <summary>
    /// Fetches an item and its topic votes.
    /// </summary>
    /// <param name="dtddId">DoesTheDogDie item id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The item detail.</returns>
    Task<DtddItemDetail> GetItemAsync(int dtddId, CancellationToken cancellationToken);

    /// <summary>
    /// Fetches the full topic taxonomy.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>All topics.</returns>
    Task<IReadOnlyList<DtddTopic>> GetTopicsAsync(CancellationToken cancellationToken);
}
