using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Jellyfin.Plugin.XtreamPro.Api.Models;
using Jellyfin.Plugin.XtreamPro.Configuration;
using Jellyfin.Plugin.XtreamPro.LiveTv;
using Jellyfin.Plugin.XtreamPro.Sync;

namespace Jellyfin.Plugin.XtreamPro.CatchUp;

/// <summary>
/// Turns past programmes on archive channels into a TV library: a show per channel, an episode per programme.
/// </summary>
public class CatchUpPlanner
{
    private readonly StreamUrls _urls;
    private readonly TimeZoneInfo _displayZone;

    /// <summary>
    /// Initializes a new instance of the <see cref="CatchUpPlanner"/> class.
    /// </summary>
    /// <param name="urls">Stream URL builder.</param>
    /// <param name="displayZone">Time zone for file names and air dates.</param>
    public CatchUpPlanner(StreamUrls urls, TimeZoneInfo displayZone)
    {
        _urls = urls;
        _displayZone = displayZone;
    }

    /// <summary>
    /// Plans catch-up files for one provider.
    /// </summary>
    /// <param name="profile">Provider profile.</param>
    /// <param name="channels">Live streams.</param>
    /// <param name="guide">The provider's stored guide.</param>
    /// <param name="now">Current time.</param>
    /// <returns>The planned items.</returns>
    public IReadOnlyList<SyncItem> Plan(ProviderProfile profile, IEnumerable<LiveChannel> channels, GuideResult guide, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(guide);
        var items = new List<SyncItem>();
        foreach (var channel in channels.Where(c => c.TvArchive && c.TvArchiveDuration > 0))
        {
            var windowStart = now.AddDays(-channel.TvArchiveDuration!.Value);
            var past = guide.Programmes.GetValueOrDefault(ChannelId.Format(profile.Id, channel.StreamId), [])
                .Where(p => p.Start >= windowStart && p.Stop <= now)
                .DistinctBy(p => p.Start)
                .ToList();
            if (past.Count == 0)
            {
                continue;
            }

            var show = new SeriesEntry { SeriesId = channel.StreamId, Title = LibraryNaming.Sanitize(channel.Name ?? channel.StreamId.ToString(CultureInfo.InvariantCulture)), Poster = channel.StreamIcon };
            var key = "catchup:" + channel.StreamId.ToString(CultureInfo.InvariantCulture);
            items.Add(new SyncItem(key + ":channel", new Dictionary<string, string> { [$"{show.Title}/tvshow.nfo"] = NfoWriter.TvShow(show) }));

            foreach (var programme in past)
            {
                var local = TimeZoneInfo.ConvertTime(programme.Start, _displayZone);
                var minutes = (int)Math.Ceiling((programme.Stop!.Value - programme.Start).TotalMinutes);
                var episode = new EpisodeEntry
                {
                    Id = channel.StreamId,
                    Season = local.Year,
                    Episode = int.Parse(local.ToString("MMddHHmm", CultureInfo.InvariantCulture), CultureInfo.InvariantCulture),
                    Title = programme.Title,
                    Extension = "ts",
                    Plot = programme.Description,
                    AirDate = local.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    Thumb = programme.Icon,
                };
                var name = LibraryNaming.Sanitize($"{show.Title} - {local:yyyy-MM-dd HH.mm}{(programme.Title is null ? string.Empty : " - " + programme.Title)}");
                var path = string.Create(CultureInfo.InvariantCulture, $"{show.Title}/Season {local.Year}/{name}");
                items.Add(new SyncItem(
                    key + ":" + programme.Start.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture),
                    new Dictionary<string, string>
                    {
                        [path + ".strm"] = _urls.ForCatchUp(profile.Id, channel.StreamId, programme.Start, minutes),
                        [path + ".nfo"] = NfoWriter.Episode(show, episode),
                    }));
            }
        }

        return items;
    }
}
