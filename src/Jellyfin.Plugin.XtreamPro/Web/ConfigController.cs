using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.XtreamPro.Api;
using Jellyfin.Plugin.XtreamPro.Api.Models;
using Jellyfin.Plugin.XtreamPro.Configuration;
using Jellyfin.Plugin.XtreamPro.LiveTv;
using Jellyfin.Plugin.XtreamPro.Sync;
using Jellyfin.Plugin.XtreamPro.Tasks;
using MediaBrowser.Common.Api;
using MediaBrowser.Model.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.XtreamPro.Web;

/// <summary>
/// Endpoints behind the plugin's dashboard page.
/// </summary>
[ApiController]
[Authorize(Policy = Policies.RequiresElevation)]
[Route("XtreamPro")]
public class ConfigController : ControllerBase
{
    private const string TaskCategory = "Xtream Pro";

    private readonly XtreamClientFactory _clients;
    private readonly ConnectionTracker _connections;
    private readonly LiveCatalog _catalog;
    private readonly GuideStore _guides;
    private readonly ITaskManager _tasks;
    private readonly SyncStatusStore _status;
    private readonly EnhancedUiStatus _enhancedUi;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigController"/> class.
    /// </summary>
    /// <param name="clients">Client factory.</param>
    /// <param name="connections">Connection tracker.</param>
    /// <param name="catalog">Live channel catalog.</param>
    /// <param name="guides">Guide store.</param>
    /// <param name="tasks">Task manager.</param>
    /// <param name="status">Sync status store.</param>
    /// <param name="enhancedUi">Enhanced Live TV link status.</param>
    public ConfigController(XtreamClientFactory clients, ConnectionTracker connections, LiveCatalog catalog, GuideStore guides, ITaskManager tasks, SyncStatusStore status, EnhancedUiStatus enhancedUi)
    {
        _enhancedUi = enhancedUi;
        _clients = clients;
        _connections = connections;
        _catalog = catalog;
        _guides = guides;
        _tasks = tasks;
        _status = status;
    }

    /// <summary>
    /// Tests a provider login, saved or not.
    /// </summary>
    /// <param name="profile">The provider details from the form.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Account status and connection usage, or why the test failed.</returns>
    [HttpPost("Test")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ConnectionTestResult> TestConnection([FromBody] ProviderProfile profile, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(profile);
        try
        {
            var account = await _clients.Create(profile).GetAccountInfoAsync(cancellationToken).ConfigureAwait(false);
            if (!account.UserInfo.Auth)
            {
                return new ConnectionTestResult(false, Error: "The provider rejected the username or password.");
            }

            var user = account.UserInfo;
            return new ConnectionTestResult(true, user.Status, user.ExpiresAt, user.IsTrial, user.MaxConnections, user.ActiveConnections, _connections.Active(profile.Id));
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or ArgumentException or Polly.CircuitBreaker.BrokenCircuitException or TaskCanceledException)
        {
            return new ConnectionTestResult(false, Error: ex.Message);
        }
    }

    /// <summary>
    /// Lists a provider's categories for one content type.
    /// </summary>
    /// <param name="type"><c>live</c>, <c>vod</c> or <c>series</c>.</param>
    /// <param name="profile">The provider details from the form.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The categories in provider order.</returns>
    [HttpPost("Categories/{type}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<CategoryOption>>> Categories([FromRoute] string type, [FromBody] ProviderProfile profile, CancellationToken cancellationToken)
    {
        var client = _clients.Create(profile);
        Func<CancellationToken, Task<IReadOnlyList<Category>>>? fetch = type switch
        {
            "live" => client.GetLiveCategoriesAsync,
            "vod" => client.GetVodCategoriesAsync,
            "series" => client.GetSeriesCategoriesAsync,
            _ => null,
        };
        if (fetch is null)
        {
            return BadRequest();
        }

        var categories = await fetch(cancellationToken).ConfigureAwait(false);
        return categories
            .Where(c => c.CategoryId is not null)
            .Select(c => new CategoryOption(c.CategoryId!, c.CategoryName ?? c.CategoryId!))
            .ToList();
    }

    /// <summary>
    /// Cleans sample titles with the given rules, for the live preview.
    /// </summary>
    /// <param name="request">Rules and titles.</param>
    /// <returns>Each title cleaned.</returns>
    [HttpPost("TitlePreview")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IReadOnlyList<TitlePreview> PreviewTitles([FromBody] TitlePreviewRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var cleaner = new TitleCleaner(request.Rules);
        return request.Titles.Select(t =>
        {
            var (title, year) = cleaner.Clean(t);
            return new TitlePreview(t, title, year);
        }).ToList();
    }

    /// <summary>
    /// Lists the plugin's scheduled tasks with their last results.
    /// </summary>
    /// <returns>The tasks.</returns>
    [HttpGet("Status")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IReadOnlyList<TaskStatus> Status()
        => _tasks.ScheduledTasks
            .Where(t => t.Category == TaskCategory)
            .Select(t => new TaskStatus(
                t.Id,
                t.ScheduledTask.Key,
                t.Name,
                t.State.ToString(),
                t.LastExecutionResult?.EndTimeUtc,
                t.LastExecutionResult?.Status.ToString(),
                t.LastExecutionResult?.ErrorMessage,
                _status.All.GetValueOrDefault(t.ScheduledTask.Key)?.Report))
            .ToList();

    /// <summary>
    /// Gets whether the web client links to the enhanced Live TV page.
    /// </summary>
    /// <returns>The link status.</returns>
    [HttpGet("EnhancedUi")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public EnhancedUiStatus EnhancedUi() => _enhancedUi;

    /// <summary>
    /// Lists live channels without guide data, per provider.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Guide coverage per provider.</returns>
    [HttpGet("GuideHealth")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<GuideHealth>> GuideHealth(CancellationToken cancellationToken)
    {
        var health = new List<GuideHealth>();
        foreach (var profile in _catalog.Providers)
        {
            var streams = await _catalog.GetStreamsAsync(profile, cancellationToken).ConfigureAwait(false);
            var missing = (await _guides.LoadAsync(profile.Id, cancellationToken).ConfigureAwait(false))?.ChannelsWithoutGuide.ToHashSet() ?? [];
            health.Add(new GuideHealth(
                profile.Id,
                profile.Name,
                streams.Count,
                streams
                    .Where(s => missing.Contains(ChannelId.Format(profile.Id, s.StreamId)))
                    .Select(s => new MissingGuide(s.StreamId, s.Name, s.EpgChannelId))
                    .ToList()));
        }

        return health;
    }
}
