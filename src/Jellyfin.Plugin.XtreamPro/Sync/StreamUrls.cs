using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
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
        => string.Create(CultureInfo.InvariantCulture, $"{_baseUrl}/XtreamPro/Stream/{providerId:N}/{Kind(kind)}/{id}.{extension}?sig={Sign(providerId, kind, id)}");

    /// <summary>
    /// Checks a signature from a stream URL.
    /// </summary>
    /// <param name="providerId">Provider profile id.</param>
    /// <param name="kind">Stream kind.</param>
    /// <param name="id">Provider stream id.</param>
    /// <param name="signature">Signature from the URL.</param>
    /// <returns>True if the URL was issued by this server.</returns>
    public bool Verify(Guid providerId, StreamKind kind, int id, string? signature)
    {
        if (string.IsNullOrEmpty(signature))
        {
            return false;
        }

        try
        {
            return CryptographicOperations.FixedTimeEquals(
                WebEncoders.Base64UrlDecode(signature),
                WebEncoders.Base64UrlDecode(Sign(providerId, kind, id)));
        }
        catch (FormatException)
        {
            return false;
        }
    }

    /// <summary>
    /// Gets the URL segment for a kind.
    /// </summary>
    /// <param name="kind">Stream kind.</param>
    /// <returns>The segment.</returns>
    public static string Kind(StreamKind kind) => kind == StreamKind.Movie ? "movie" : "episode";

    private string Sign(Guid providerId, StreamKind kind, int id)
    {
        var payload = Encoding.UTF8.GetBytes(string.Create(CultureInfo.InvariantCulture, $"{providerId:N}/{Kind(kind)}/{id}"));
        return WebEncoders.Base64UrlEncode(HMACSHA256.HashData(_secret, payload)[..16]);
    }
}
