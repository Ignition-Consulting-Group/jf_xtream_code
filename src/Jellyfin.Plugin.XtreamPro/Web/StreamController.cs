using System;
using System.Globalization;
using System.IO;
using System.Linq;
using Jellyfin.Plugin.XtreamPro.Api;
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
    private readonly IXtreamSettings _settings;
    private readonly XtreamClientFactory _clients;

    /// <summary>
    /// Initializes a new instance of the <see cref="StreamController"/> class.
    /// </summary>
    /// <param name="settings">Plugin settings.</param>
    /// <param name="clients">Client factory.</param>
    public StreamController(IXtreamSettings settings, XtreamClientFactory clients)
    {
        _settings = settings;
        _clients = clients;
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
        var config = _settings.Current;
        if (streamKind is null
            || !Guid.TryParseExact(providerId, "N", out var id)
            || !int.TryParse(Path.GetFileNameWithoutExtension(file), NumberStyles.None, CultureInfo.InvariantCulture, out var streamId)
            || config.Providers.FirstOrDefault(p => p.Id == id) is not { } profile
            || string.IsNullOrEmpty(config.StreamSecret))
        {
            return NotFound();
        }

        if (!new StreamUrls(string.Empty, config.StreamSecret).Verify(id, streamKind.Value, streamId, sig))
        {
            return StatusCode(StatusCodes.Status403Forbidden);
        }

        var extension = Path.GetExtension(file).TrimStart('.');
        var urls = _clients.Create(profile).Urls;
        var target = streamKind == StreamKind.Movie ? urls.Movie(streamId, extension) : urls.Episode(streamId, extension);
        return Redirect(target.AbsoluteUri);
    }
}
