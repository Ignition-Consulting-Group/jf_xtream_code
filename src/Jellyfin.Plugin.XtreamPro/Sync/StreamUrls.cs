using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Jellyfin.Plugin.XtreamPro.Configuration;
using Microsoft.AspNetCore.WebUtilities;

namespace Jellyfin.Plugin.XtreamPro.Sync;

/// <summary>
/// Builds and checks the signed plugin URLs written into <c>.strm</c> files, so files never hold provider credentials.
/// </summary>
public class StreamUrls
{
    private readonly string _baseUrl;
    private readonly byte[] _secret;

    /// <summary>
    /// Initializes a new instance of the <see cref="StreamUrls"/> class.
    /// </summary>
    /// <param name="baseUrl">Jellyfin address that clients and the server can reach.</param>
    /// <param name="secret">Base64 signing secret.</param>
    public StreamUrls(string baseUrl, string secret)
    {
        ArgumentNullException.ThrowIfNull(baseUrl);
        _baseUrl = baseUrl.TrimEnd('/');
        _secret = Convert.FromBase64String(secret);
    }

    /// <summary>
    /// Creates the URL builder from plugin settings, generating and saving the signing secret on first use.
    /// </summary>
    /// <param name="settings">Plugin settings.</param>
    /// <param name="localServerUrl">Gets the server's local address, used when no stream base URL is set.</param>
    /// <returns>The URL builder.</returns>
    public static StreamUrls FromSettings(IXtreamSettings settings, Func<string> localServerUrl)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(localServerUrl);
        var config = settings.Current;
        if (string.IsNullOrEmpty(config.StreamSecret))
        {
            config.StreamSecret = NewSecret();
            settings.Save();
        }

        return new StreamUrls(string.IsNullOrWhiteSpace(config.StreamBaseUrl) ? localServerUrl() : config.StreamBaseUrl, config.StreamSecret);
    }

    /// <summary>
    /// Creates a new random signing secret.
    /// </summary>
    /// <returns>A base64 secret.</returns>
    public static string NewSecret() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    /// <summary>
    /// Builds the URL for a stream.
    /// </summary>
    /// <param name="providerId">Provider profile id.</param>
    /// <param name="kind">Stream kind.</param>
    /// <param name="id">Provider stream id.</param>
    /// <param name="extension">Container extension.</param>
    /// <returns>The URL.</returns>
    public string For(Guid providerId, StreamKind kind, int id, string extension)
        => string.Create(CultureInfo.InvariantCulture, $"{_baseUrl}/XtreamPro/Stream/{providerId:N}/{Kind(kind)}/{id}.{extension}?sig={Sign(Payload(providerId, kind, id))}");

    /// <summary>
    /// Builds the URL for a past programme on a catch-up channel.
    /// </summary>
    /// <param name="providerId">Provider profile id.</param>
    /// <param name="streamId">Live stream id.</param>
    /// <param name="start">Programme start.</param>
    /// <param name="minutes">Programme length in minutes.</param>
    /// <returns>The URL.</returns>
    public string ForCatchUp(Guid providerId, int streamId, DateTimeOffset start, int minutes)
    {
        var unix = start.ToUnixTimeSeconds();
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{_baseUrl}/XtreamPro/Stream/{providerId:N}/catchup/{streamId}.ts?start={unix}&minutes={minutes}&sig={Sign(CatchUpPayload(providerId, streamId, unix, minutes))}");
    }

    /// <summary>
    /// Checks a signature from a catch-up URL.
    /// </summary>
    /// <param name="providerId">Provider profile id.</param>
    /// <param name="streamId">Live stream id.</param>
    /// <param name="start">Start as a unix timestamp.</param>
    /// <param name="minutes">Length in minutes.</param>
    /// <param name="signature">Signature from the URL.</param>
    /// <returns>True if the URL was issued by this server.</returns>
    public bool VerifyCatchUp(Guid providerId, int streamId, long start, int minutes, string? signature)
        => Matches(CatchUpPayload(providerId, streamId, start, minutes), signature);

    /// <summary>
    /// Checks a signature from a stream URL.
    /// </summary>
    /// <param name="providerId">Provider profile id.</param>
    /// <param name="kind">Stream kind.</param>
    /// <param name="id">Provider stream id.</param>
    /// <param name="signature">Signature from the URL.</param>
    /// <returns>True if the URL was issued by this server.</returns>
    public bool Verify(Guid providerId, StreamKind kind, int id, string? signature)
        => Matches(Payload(providerId, kind, id), signature);

    /// <summary>
    /// Gets the URL segment for a kind.
    /// </summary>
    /// <param name="kind">Stream kind.</param>
    /// <returns>The segment.</returns>
    public static string Kind(StreamKind kind) => kind == StreamKind.Movie ? "movie" : "episode";

    private static string Payload(Guid providerId, StreamKind kind, int id)
        => string.Create(CultureInfo.InvariantCulture, $"{providerId:N}/{Kind(kind)}/{id}");

    private static string CatchUpPayload(Guid providerId, int streamId, long start, int minutes)
        => string.Create(CultureInfo.InvariantCulture, $"{providerId:N}/catchup/{streamId}/{start}/{minutes}");

    private bool Matches(string payload, string? signature)
    {
        if (string.IsNullOrEmpty(signature))
        {
            return false;
        }

        try
        {
            return CryptographicOperations.FixedTimeEquals(
                WebEncoders.Base64UrlDecode(signature),
                WebEncoders.Base64UrlDecode(Sign(payload)));
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private string Sign(string payload)
        => WebEncoders.Base64UrlEncode(HMACSHA256.HashData(_secret, Encoding.UTF8.GetBytes(payload))[..16]);
}
