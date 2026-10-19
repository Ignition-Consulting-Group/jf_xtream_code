using System.Collections.Generic;
using Jellyfin.Plugin.XtreamPro.Configuration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.XtreamPro.Web;

/// <summary>
/// Serves the enhanced Live TV page. The page signs in with the user's existing web client session, so the files themselves are public.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("XtreamPro/Web")]
public class EnhancedUiController : ControllerBase
{
    private static readonly Dictionary<string, string> Assets = new()
    {
        [string.Empty] = "text/html; charset=utf-8",
        ["livetv.js"] = "text/javascript; charset=utf-8",
        ["livetv.css"] = "text/css; charset=utf-8",
        ["inject.js"] = "text/javascript; charset=utf-8",
        ["hls.light.min.js"] = "text/javascript; charset=utf-8",
    };

    private readonly IXtreamSettings _settings;

    /// <summary>
    /// Initializes a new instance of the <see cref="EnhancedUiController"/> class.
    /// </summary>
    /// <param name="settings">Plugin settings.</param>
    public EnhancedUiController(IXtreamSettings settings)
    {
        _settings = settings;
    }

    /// <summary>
    /// Gets a page asset.
    /// </summary>
    /// <param name="file">Asset name, or empty for the page.</param>
    /// <returns>The asset.</returns>
    [HttpGet("{file?}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult Asset([FromRoute] string? file)
    {
        file ??= string.Empty;
        if (!Assets.TryGetValue(file, out var contentType))
        {
            return NotFound();
        }

        if (!_settings.Current.EnableEnhancedLiveTv)
        {
            // The loader may still be injected until Jellyfin restarts; it must then do nothing.
            return file == "inject.js" ? Content(string.Empty, contentType) : NotFound();
        }

        var resource = $"{typeof(EnhancedUiController).Namespace}.Assets.{(file.Length == 0 ? "index.html" : file)}";
        var stream = typeof(EnhancedUiController).Assembly.GetManifestResourceStream(resource);
        return stream is null ? NotFound() : File(stream, contentType);
    }
}
