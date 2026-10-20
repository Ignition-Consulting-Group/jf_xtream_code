using System.Collections.Generic;
using Jellyfin.Plugin.XtreamPro.Api;
using Jellyfin.Plugin.XtreamPro.Api.Models;
using Jellyfin.Plugin.XtreamPro.Configuration;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.MediaInfo;

namespace Jellyfin.Plugin.XtreamPro.LiveTv;

/// <summary>
/// Builds the media source for a provider live stream.
/// </summary>
internal static class LiveMediaSource
{
    public static MediaSourceInfo Create(XtreamClient client, ProviderProfile profile, LiveChannel channel, string channelId)
    {
        var hls = profile.LiveFormat == LiveFormat.Hls;
        var source = new MediaSourceInfo
        {
            Id = channelId,
            Path = client.Urls.Live(channel.StreamId, profile.LiveFormat).AbsoluteUri,
            Protocol = MediaProtocol.Http,
            Container = hls ? "hls" : "ts",
            MediaStreams =
            [
                new MediaStream { Type = MediaStreamType.Video, Index = -1, IsInterlaced = true },
                new MediaStream { Type = MediaStreamType.Audio, Index = -1 },
            ],
            RequiresOpening = true,
            RequiresClosing = true,
            IsInfiniteStream = true,
            IsRemote = true,
            SupportsDirectPlay = profile.PreferDirectPlay && !hls,
            SupportsDirectStream = true,
            SupportsTranscoding = true,
            RequiredHttpHeaders = new Dictionary<string, string> { ["User-Agent"] = client.UserAgent },
        };
        source.InferTotalBitrate();
        return source;
    }
}
