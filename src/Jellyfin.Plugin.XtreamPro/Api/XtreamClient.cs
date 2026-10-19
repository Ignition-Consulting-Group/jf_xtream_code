using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.XtreamPro.Api.Models;

namespace Jellyfin.Plugin.XtreamPro.Api;

/// <summary>
/// Client for one Xtream provider's <c>player_api.php</c> and <c>xmltv.php</c>.
/// </summary>
public class XtreamClient
{
    private readonly HttpClient _http;

    /// <summary>
    /// Initializes a new instance of the <see cref="XtreamClient"/> class.
    /// </summary>
    /// <param name="http">HTTP client with resilience already applied.</param>
    /// <param name="credentials">Provider credentials.</param>
    /// <param name="userAgent">User-Agent sent with every request.</param>
    public XtreamClient(HttpClient http, XtreamCredentials credentials, string userAgent)
    {
        _http = http;
        UserAgent = userAgent;
        Urls = new XtreamUrlBuilder(credentials);
    }

    /// <summary>
    /// Gets the User-Agent sent to this provider.
    /// </summary>
    public string UserAgent { get; }

    /// <summary>
    /// Gets the URL builder for this provider.
    /// </summary>
    public XtreamUrlBuilder Urls { get; }

    /// <summary>Gets account and server info.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The account info.</returns>
    public Task<AccountInfo> GetAccountInfoAsync(CancellationToken cancellationToken)
        => GetAsync<AccountInfo>(Urls.PlayerApi(), cancellationToken);

    /// <summary>Gets live categories.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The categories.</returns>
    public Task<IReadOnlyList<Category>> GetLiveCategoriesAsync(CancellationToken cancellationToken)
        => GetAsync<IReadOnlyList<Category>>(Urls.PlayerApi("get_live_categories"), cancellationToken);

    /// <summary>Gets VOD categories.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The categories.</returns>
    public Task<IReadOnlyList<Category>> GetVodCategoriesAsync(CancellationToken cancellationToken)
        => GetAsync<IReadOnlyList<Category>>(Urls.PlayerApi("get_vod_categories"), cancellationToken);

    /// <summary>Gets series categories.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The categories.</returns>
    public Task<IReadOnlyList<Category>> GetSeriesCategoriesAsync(CancellationToken cancellationToken)
        => GetAsync<IReadOnlyList<Category>>(Urls.PlayerApi("get_series_categories"), cancellationToken);

    /// <summary>Gets live channels.</summary>
    /// <param name="categoryId">Category to filter by, or null for all.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The channels.</returns>
    public Task<IReadOnlyList<LiveChannel>> GetLiveChannelsAsync(string? categoryId, CancellationToken cancellationToken)
        => GetAsync<IReadOnlyList<LiveChannel>>(ByCategory("get_live_streams", categoryId), cancellationToken);

    /// <summary>Gets movies.</summary>
    /// <param name="categoryId">Category to filter by, or null for all.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The movies.</returns>
    public Task<IReadOnlyList<VodItem>> GetVodItemsAsync(string? categoryId, CancellationToken cancellationToken)
        => GetAsync<IReadOnlyList<VodItem>>(ByCategory("get_vod_streams", categoryId), cancellationToken);

    /// <summary>Gets series.</summary>
    /// <param name="categoryId">Category to filter by, or null for all.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The series.</returns>
    public Task<IReadOnlyList<Series>> GetSeriesAsync(string? categoryId, CancellationToken cancellationToken)
        => GetAsync<IReadOnlyList<Series>>(ByCategory("get_series", categoryId), cancellationToken);

    /// <summary>Gets movie details.</summary>
    /// <param name="vodId">Movie stream id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The details.</returns>
    public Task<VodInfo> GetVodInfoAsync(int vodId, CancellationToken cancellationToken)
        => GetAsync<VodInfo>(Urls.PlayerApi("get_vod_info", ("vod_id", Invariant(vodId))), cancellationToken);

    /// <summary>Gets series details and episodes.</summary>
    /// <param name="seriesId">Series id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The details.</returns>
    public Task<SeriesInfo> GetSeriesInfoAsync(int seriesId, CancellationToken cancellationToken)
        => GetAsync<SeriesInfo>(Urls.PlayerApi("get_series_info", ("series_id", Invariant(seriesId))), cancellationToken);

    /// <summary>Gets the next few programmes for a channel.</summary>
    /// <param name="streamId">Channel stream id.</param>
    /// <param name="limit">Maximum number of programmes.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The programmes.</returns>
    public Task<ShortEpg> GetShortEpgAsync(int streamId, int limit, CancellationToken cancellationToken)
        => GetAsync<ShortEpg>(
            Urls.PlayerApi("get_short_epg", ("stream_id", Invariant(streamId)), ("limit", Invariant(limit))),
            cancellationToken);

    /// <summary>Opens the full XMLTV guide for streamed parsing.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The response body. The caller disposes it.</returns>
    public async Task<Stream> OpenXmltvAsync(CancellationToken cancellationToken)
    {
        var response = await SendAsync(Urls.Xmltv(), cancellationToken).ConfigureAwait(false);
        return await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
    }

    private static string Invariant(int value) => value.ToString(CultureInfo.InvariantCulture);

    private Uri ByCategory(string action, string? categoryId)
        => categoryId is null ? Urls.PlayerApi(action) : Urls.PlayerApi(action, ("category_id", categoryId));

    private async Task<T> GetAsync<T>(Uri uri, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(uri, cancellationToken).ConfigureAwait(false);
        return (await response.Content.ReadFromJsonAsync<T>(XtreamJson.Options, cancellationToken).ConfigureAwait(false))!;
    }

    private async Task<HttpResponseMessage> SendAsync(Uri uri, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.TryAddWithoutValidation("User-Agent", UserAgent);
        var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        try
        {
            response.EnsureSuccessStatusCode();
        }
        catch
        {
            response.Dispose();
            throw;
        }

        return response;
    }
}
