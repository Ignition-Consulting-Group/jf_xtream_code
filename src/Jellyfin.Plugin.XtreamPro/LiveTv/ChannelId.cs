using System;
using System.Globalization;

namespace Jellyfin.Plugin.XtreamPro.LiveTv;

/// <summary>
/// Formats and parses the Jellyfin channel ids used for Xtream live streams.
/// </summary>
public static class ChannelId
{
    /// <summary>
    /// Tuner host id and channel id prefix for this plugin.
    /// </summary>
    public const string TunerHostId = "xtreampro";

    private const string Prefix = TunerHostId + "_";

    /// <summary>
    /// Formats a channel id.
    /// </summary>
    /// <param name="providerId">Provider profile id.</param>
    /// <param name="streamId">Provider stream id.</param>
    /// <returns>The channel id.</returns>
    public static string Format(Guid providerId, int streamId)
        => string.Create(CultureInfo.InvariantCulture, $"{Prefix}{providerId:N}_{streamId}");

    /// <summary>
    /// Parses a channel id created by <see cref="Format"/>.
    /// </summary>
    /// <param name="channelId">The channel id.</param>
    /// <param name="parsed">The provider and stream ids.</param>
    /// <returns>True if the id belongs to this plugin.</returns>
    public static bool TryParse(string? channelId, out (Guid ProviderId, int StreamId) parsed)
    {
        parsed = default;
        if (channelId is null || !channelId.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return false;
        }

        var parts = channelId[Prefix.Length..].Split('_');
        if (parts.Length != 2
            || !Guid.TryParseExact(parts[0], "N", out var provider)
            || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var stream))
        {
            return false;
        }

        parsed = (provider, stream);
        return true;
    }
}
