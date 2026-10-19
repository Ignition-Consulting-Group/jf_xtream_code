using System;
using System.Globalization;
using System.Linq;

namespace Jellyfin.Plugin.XtreamPro.Api;

/// <summary>
/// Builds API and stream URLs for an Xtream provider.
/// </summary>
public class XtreamUrlBuilder
{
    private readonly Uri _base;
    private readonly string _user;
    private readonly string _pass;
    private readonly string _query;

    /// <summary>
    /// Initializes a new instance of the <see cref="XtreamUrlBuilder"/> class.
    /// </summary>
    /// <param name="credentials">Provider credentials.</param>
    public XtreamUrlBuilder(XtreamCredentials credentials)
    {
        ArgumentNullException.ThrowIfNull(credentials);
        _base = credentials.BaseUrl.AbsoluteUri.EndsWith('/') ? credentials.BaseUrl : new Uri(credentials.BaseUrl.AbsoluteUri + "/");
        _user = Uri.EscapeDataString(credentials.Username);
        _pass = Uri.EscapeDataString(credentials.Password);
        _query = $"username={_user}&password={_pass}";
    }

    /// <summary>
    /// Gets the <c>player_api.php</c> URL. Without an action it returns account info.
    /// </summary>
    /// <param name="action">API action, or null for account info.</param>
    /// <param name="parameters">Extra query parameters.</param>
    /// <returns>The URL.</returns>
    public Uri PlayerApi(string? action = null, params (string Name, string Value)[] parameters)
    {
        var query = _query;
        if (action is not null)
        {
            query += "&action=" + Uri.EscapeDataString(action);
        }

        query += string.Concat(parameters.Select(p => $"&{Uri.EscapeDataString(p.Name)}={Uri.EscapeDataString(p.Value)}"));
        return new Uri(_base, "player_api.php?" + query);
    }

    /// <summary>
    /// Gets the full XMLTV guide URL.
    /// </summary>
    /// <returns>The URL.</returns>
    public Uri Xmltv() => new(_base, "xmltv.php?" + _query);

    /// <summary>
    /// Gets a live stream URL.
    /// </summary>
    /// <param name="streamId">Provider stream id.</param>
    /// <param name="format">Output format.</param>
    /// <returns>The URL.</returns>
    public Uri Live(int streamId, LiveFormat format) => Stream("live", streamId, format == LiveFormat.Hls ? "m3u8" : "ts");

    /// <summary>
    /// Gets a movie stream URL.
    /// </summary>
    /// <param name="streamId">Provider stream id.</param>
    /// <param name="containerExtension">Container extension from the provider.</param>
    /// <returns>The URL.</returns>
    public Uri Movie(int streamId, string containerExtension) => Stream("movie", streamId, containerExtension);

    /// <summary>
    /// Gets a series episode stream URL.
    /// </summary>
    /// <param name="episodeId">Provider episode id.</param>
    /// <param name="containerExtension">Container extension from the provider.</param>
    /// <returns>The URL.</returns>
    public Uri Episode(int episodeId, string containerExtension) => Stream("series", episodeId, containerExtension);

    /// <summary>
    /// Gets a catch-up URL for a past programme.
    /// </summary>
    /// <param name="streamId">Provider stream id.</param>
    /// <param name="start">Programme start in the provider's timezone.</param>
    /// <param name="duration">Programme length.</param>
    /// <returns>The URL.</returns>
    public Uri Timeshift(int streamId, DateTime start, TimeSpan duration)
    {
        var minutes = (int)Math.Ceiling(duration.TotalMinutes);
        var path = string.Create(
            CultureInfo.InvariantCulture,
            $"timeshift/{_user}/{_pass}/{minutes}/{start:yyyy-MM-dd:HH-mm}/{streamId}.ts");
        return new Uri(_base, path);
    }

    private Uri Stream(string kind, int id, string extension)
        => new(_base, string.Create(CultureInfo.InvariantCulture, $"{kind}/{_user}/{_pass}/{id}.{extension}"));
}
