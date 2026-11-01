using System.Net.Mime;
using Jellyfin.Plugin.ContentWarnings.Api;
using Jellyfin.Plugin.ContentWarnings.Dtdd;
using Jellyfin.Plugin.ContentWarnings.Services;
using MediaBrowser.Common.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.ContentWarnings.Api;

/// <summary>
/// Content warnings REST API.
/// </summary>
[ApiController]
[Authorize]
[Route("ContentWarnings")]
[Produces(MediaTypeNames.Application.Json)]
public sealed class ContentWarningsController : ControllerBase
{
    private readonly ContentWarningService _service;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContentWarningsController"/> class.
    /// </summary>
    /// <param name="service">Content warning service.</param>
    public ContentWarningsController(ContentWarningService service)
    {
        _service = service;
    }

    /// <summary>
    /// Gets the cached warnings for one item.
    /// </summary>
    /// <param name="itemId">Jellyfin item id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The warnings.</returns>
    [HttpGet("Items/{itemId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<ItemWarningsResponse>> GetItemWarnings(
        [FromRoute] Guid itemId,
        CancellationToken cancellationToken)
    {
        return Ok(await _service.GetItemWarningsAsync(itemId, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// Forces a match and refresh for one item.
    /// </summary>
    /// <param name="itemId">Jellyfin item id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The refreshed warnings.</returns>
    [HttpPost("Items/{itemId:guid}/Refresh")]
    [Authorize(Policy = Policies.RequiresElevation)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<ItemWarningsResponse>> RefreshItem(
        [FromRoute] Guid itemId,
        CancellationToken cancellationToken)
    {
        try
        {
            await _service.ProcessItemAsync(itemId, force: true, cancellationToken).ConfigureAwait(false);
            return Ok(await _service.GetItemWarningsAsync(itemId, cancellationToken).ConfigureAwait(false));
        }
        catch (DtddNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (DtddException ex)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new { message = ex.Message });
        }
    }

    /// <summary>
    /// Sets a manual DoesTheDogDie id for one item.
    /// </summary>
    /// <param name="itemId">Jellyfin item id.</param>
    /// <param name="request">Manual id, or null for "no match, do not retry".</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The resulting warnings.</returns>
    [HttpPut("Items/{itemId:guid}/Match")]
    [Authorize(Policy = Policies.RequiresElevation)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<ItemWarningsResponse>> SetManualMatch(
        [FromRoute] Guid itemId,
        [FromBody] ManualMatchRequest request,
        CancellationToken cancellationToken)
    {
        await _service.SetManualMatchAsync(itemId, request.DtddId, cancellationToken).ConfigureAwait(false);

        if (request.DtddId.HasValue)
        {
            try
            {
                await _service.ProcessItemAsync(itemId, force: true, cancellationToken).ConfigureAwait(false);
            }
            catch (DtddNotFoundException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (DtddException ex)
            {
                return StatusCode(StatusCodes.Status502BadGateway, new { message = ex.Message });
            }
        }

        return Ok(await _service.GetItemWarningsAsync(itemId, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// Lists every known topic.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The topics.</returns>
    [HttpGet("Topics")]
    [Authorize(Policy = Policies.RequiresElevation)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TopicResponse>>> GetTopics(CancellationToken cancellationToken)
    {
        try
        {
            var topics = await _service.GetTopicsAsync(force: false, cancellationToken).ConfigureAwait(false);
            return Ok(topics
                .Select(topic => new TopicResponse(
                    topic.Id,
                    topic.Name,
                    topic.DoesName,
                    topic.Keywords,
                    Core.DefaultTopics.IsDefault(topic.Name, topic.DoesName)))
                .ToList());
        }
        catch (DtddException ex)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new { message = ex.Message });
        }
    }

    /// <summary>
    /// Lists items with no match.
    /// </summary>
    /// <returns>The unmatched items.</returns>
    [HttpGet("Unmatched")]
    [Authorize(Policy = Policies.RequiresElevation)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<UnmatchedItemResponse>> GetUnmatched()
    {
        return Ok(_service.GetUnmatchedItems());
    }

    /// <summary>
    /// Tests the configured API key.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result.</returns>
    [HttpPost("TestKey")]
    [Authorize(Policy = Policies.RequiresElevation)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<TestConnectionResponse>> TestKey(CancellationToken cancellationToken)
    {
        return Ok(await _service.TestConnectionAsync(cancellationToken).ConfigureAwait(false));
    }
}
