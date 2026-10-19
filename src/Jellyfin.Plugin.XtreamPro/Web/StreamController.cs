using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.XtreamPro.Api;
using Jellyfin.Plugin.XtreamPro.CatchUp;
using Jellyfin.Plugin.XtreamPro.Configuration;
using Jellyfin.Plugin.XtreamPro.Sync;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.XtreamPro.Web;

/// <summary>
/// Redirects signed <c>.strm</c> URLs to the provider at play time, so credentials stay out of the library.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("XtreamPro/Stream")]
public class StreamController : ControllerBase
{
    private static readonly TimeSpan AccountMaxAge = TimeSpan.FromHours(6);

    private readonly IXtreamSettings _settings;
    private readonly XtreamClientFactory _clients;
    private readonly AccountMonitor _accounts;

    /// <summary>
    /// Initializes a new instance of the <see cref="StreamController"/> class.
    /// </summary>
    /// <param name="settings">Plugin settings.</param>
    /// <param name="clients">Client factory.</param>
    /// <param name="accounts">Account info cache, for the provider time zone.</param>
    public StreamController(IXtreamSettings settings, XtreamClientFactory clients, AccountMonitor accounts)
    {
        _settings = settings;
        _clients = clients;
        _accounts = accounts;
    }

    /// <summary>
    /// Redirects to a past programme on a catch-up channel.
    /// </summary>
    /// <param name="providerId">Provider profile id.</param>
    /// <param name="file">Live stream id and extension, for example <c>101.ts</c>.</param>
    /// <param name="start">Programme start as a unix timestamp.</param>
    /// <param name="minutes">Programme length in minutes.</param>
    /// <param name="sig">URL signature.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A redirect to the provider's timeshift URL.</returns>
    [HttpGet("{providerId}/catchup/{file}")]
    [ProducesResponseType(StatusCodes.Status302Found)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> GetCatchUp(
        [FromRoute] string providerId,
        [FromRoute] string file,
        [FromQuery] long start,
        [FromQuery] int minutes,
        [FromQuery] string? sig,
        CancellationToken cancellationToken)
    {
        if (Resolve(providerId, file) is not var (profile, streamId, secret))
        {
            return NotFound();
        }

        if (!new StreamUrls(string.Empty, secret).VerifyCatchUp(profile.Id, streamId, start, minutes, sig))
        {
            return StatusCode(StatusCodes.Status403Forbidden);
        }

        var account = await _accounts.GetAsync(profile, AccountMaxAge, cancellationToken).ConfigureAwait(false);
        var local = ProviderClock.ToProviderTime(DateTimeOffset.FromUnixTimeSeconds(start), account.ServerInfo?.Timezone);
        return Redirect(_clients.Create(profile).Urls.Timeshift(streamId, local, TimeSpan.FromMinutes(minutes)).AbsoluteUri);
    }

    /// <summary>
    /// Redirects to a movie or episode stream.
    /// </summary>
    /// <param name="providerId">Provider profile id.</param>
    /// <param name="kind"><c>movie</c> or <c>episode</c>.</param>
    /// <param name="file">Stream id and extension, for example <c>201.mkv</c>.</param>
    /// <param name="sig">URL signature.</param>
    /// <returns>A redirect to the provider.</returns>
    [HttpGet("{providerId}/{kind}/{file}")]
    [ProducesResponseType(StatusCodes.Status302Found)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult Get([FromRoute] string providerId, [FromRoute] string kind, [FromRoute] string file, [FromQuery] string? sig)
    {
        StreamKind? streamKind = kind switch
        {
            "movie" => StreamKind.Movie,
            "episode" => StreamKind.Episode,
            _ => null,
        };
        if (streamKind is null || Resolve(providerId, file) is not var (profile, streamId, secret))
        {
            return NotFound();
        }

        if (!new StreamUrls(string.Empty, secret).Verify(profile.Id, streamKind.Value, streamId, sig))
        {
            return StatusCode(StatusCodes.Status403Forbidden);
        }

        var extension = Path.GetExtension(file).TrimStart('.');
        var urls = _clients.Create(profile).Urls;
        var target = streamKind == StreamKind.Movie ? urls.Movie(streamId, extension) : urls.Episode(streamId, extension);
        return Redirect(target.AbsoluteUri);
    }

    private (ProviderProfile Profile, int StreamId, string Secret)? Resolve(string providerId, string file)
    {
        var config = _settings.Current;
        if (!Guid.TryParseExact(providerId, "N", out var id)
            || !int.TryParse(Path.GetFileNameWithoutExtension(file), NumberStyles.None, CultureInfo.InvariantCulture, out var streamId)
            || config.Providers.FirstOrDefault(p => p.Id == id) is not { } profile
            || string.IsNullOrEmpty(config.StreamSecret))
        {
            return null;
        }

        return (profile, streamId, config.StreamSecret);
    }
}
