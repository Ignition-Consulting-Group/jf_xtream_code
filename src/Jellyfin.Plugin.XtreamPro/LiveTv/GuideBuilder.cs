using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Jellyfin.Plugin.XtreamPro.Api;
using Jellyfin.Plugin.XtreamPro.Api.Models;
using Jellyfin.Plugin.XtreamPro.Configuration;
using Jellyfin.Plugin.XtreamPro.LiveTv.Xmltv;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.XtreamPro.LiveTv;

/// <summary>
/// Builds a provider's guide from XMLTV, filling channels without data from <c>get_short_epg</c>.
/// </summary>
public class GuideBuilder
{
    private const int ShortEpgLimit = 12;
    private const int MaxArchiveDays = 14;
    private static readonly TimeSpan FutureWindow = TimeSpan.FromDays(14);
    private static readonly TimeSpan DefaultLength = TimeSpan.FromHours(1);

    private readonly int _shortEpgBudget;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="GuideBuilder"/> class.
    /// </summary>
    /// <param name="shortEpgBudget">Maximum <c>get_short_epg</c> calls per build.</param>
    /// <param name="logger">Logger.</param>
    public GuideBuilder(int shortEpgBudget, ILogger logger)
    {
        _shortEpgBudget = shortEpgBudget;
        _logger = logger;
    }

    /// <summary>
    /// Builds the guide.
    /// </summary>
    /// <param name="profile">Provider profile.</param>
    /// <param name="client">Client for the provider.</param>
    /// <param name="channels">Live streams to build the guide for.</param>
    /// <param name="now">Current time.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The guide.</returns>
    public async Task<GuideResult> BuildAsync(
        ProviderProfile profile,
        XtreamClient client,
        IReadOnlyCollection<LiveChannel> channels,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(channels);

        var offset = TimeSpan.FromMinutes(profile.EpgOffsetMinutes);
        var archiveDays = Math.Min(MaxArchiveDays, channels.Where(c => c.TvArchive).Max(c => c.TvArchiveDuration) ?? 0);
        var windowStart = now.AddDays(-archiveDays);
        var windowEnd = now + FutureWindow;

        var guideIds = channels.Select(c => c.EpgChannelId).OfType<string>().ToHashSet(StringComparer.Ordinal);
        var icons = new Dictionary<string, string>(StringComparer.Ordinal);
        var byGuideId = await ReadXmltvAsync(client, guideIds, icons, offset, cancellationToken).ConfigureAwait(false);

        var programmes = new Dictionary<string, IReadOnlyList<XmltvProgramme>>();
        var withoutGuide = new List<string>();
        var budget = _shortEpgBudget;
        foreach (var channel in channels)
        {
            var id = ChannelId.Format(profile.Id, channel.StreamId);
            var list = InWindow(channel.EpgChannelId is { } guideId ? byGuideId.GetValueOrDefault(guideId) : null, windowStart, windowEnd);
            if (!list.Any(p => p.Stop > now) && budget-- > 0)
            {
                list = InWindow(await ReadShortEpgAsync(client, channel, offset, cancellationToken).ConfigureAwait(false), windowStart, windowEnd);
            }

            if (list.Count == 0)
            {
                withoutGuide.Add(id);
            }

            programmes[id] = list;
        }

        return new GuideResult(programmes, icons, withoutGuide);
    }

    private static List<XmltvProgramme> InWindow(IEnumerable<XmltvProgramme>? programmes, DateTimeOffset start, DateTimeOffset end)
    {
        var ordered = programmes?.OrderBy(p => p.Start).ToList() ?? [];
        return ordered
            .Select((p, i) => p.Stop is null ? p with { Stop = i + 1 < ordered.Count ? ordered[i + 1].Start : p.Start + DefaultLength } : p)
            .Where(p => p.Stop > start && p.Start < end)
            .ToList();
    }

    private async Task<Dictionary<string, List<XmltvProgramme>>> ReadXmltvAsync(
        XtreamClient client,
        HashSet<string> guideIds,
        Dictionary<string, string> icons,
        TimeSpan offset,
        CancellationToken cancellationToken)
    {
        var byGuideId = new Dictionary<string, List<XmltvProgramme>>(StringComparer.Ordinal);
        if (guideIds.Count == 0)
        {
            return byGuideId;
        }

        try
        {
            var stream = await client.OpenXmltvAsync(cancellationToken).ConfigureAwait(false);
            await using (stream.ConfigureAwait(false))
            {
                void OnChannel(XmltvChannel c)
                {
                    if (c.Icon is not null && guideIds.Contains(c.Id))
                    {
                        icons[c.Id] = c.Icon;
                    }
                }

                await foreach (var programme in XmltvReader.ReadAsync(stream, OnChannel, offset, cancellationToken).ConfigureAwait(false))
                {
                    if (guideIds.Contains(programme.ChannelId))
                    {
                        if (!byGuideId.TryGetValue(programme.ChannelId, out var list))
                        {
                            byGuideId[programme.ChannelId] = list = [];
                        }

                        list.Add(programme);
                    }
                }
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or XmlException or InvalidDataException)
        {
            _logger.LogWarning(ex, "Could not read the XMLTV guide, falling back to short EPG");
        }

        return byGuideId;
    }

    private async Task<IEnumerable<XmltvProgramme>> ReadShortEpgAsync(XtreamClient client, LiveChannel channel, TimeSpan offset, CancellationToken cancellationToken)
    {
        try
        {
            var epg = await client.GetShortEpgAsync(channel.StreamId, ShortEpgLimit, cancellationToken).ConfigureAwait(false);
            return epg.EpgListings
                .Where(l => l.Start is not null)
                .Select(l => new XmltvProgramme
                {
                    ChannelId = channel.EpgChannelId ?? string.Empty,
                    Start = l.Start!.Value + offset,
                    Stop = l.Stop + offset,
                    Title = l.Title,
                    Description = l.Description,
                })
                .ToList();
        }
        catch (Exception ex) when (ex is HttpRequestException or System.Text.Json.JsonException)
        {
            _logger.LogDebug(ex, "No short EPG for stream {StreamId}", channel.StreamId);
            return [];
        }
    }
}
