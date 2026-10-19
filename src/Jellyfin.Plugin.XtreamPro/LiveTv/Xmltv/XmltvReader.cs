using System;
using System.Buffers;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.IO.Pipelines;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;

namespace Jellyfin.Plugin.XtreamPro.LiveTv.Xmltv;

/// <summary>
/// Forward-only XMLTV reader. Only one element is held in memory at a time, so large guides are fine.
/// </summary>
public static class XmltvReader
{
    private static readonly string[] TimeFormats = ["yyyyMMddHHmmss", "yyyyMMddHHmm"];

    /// <summary>
    /// Reads programmes from an XMLTV document, reporting channels as they are found.
    /// </summary>
    /// <param name="stream">XMLTV body, plain or gzipped.</param>
    /// <param name="onChannel">Called for each channel element.</param>
    /// <param name="offset">Correction added to every time.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The programmes in document order.</returns>
    public static async IAsyncEnumerable<XmltvProgramme> ReadAsync(
        Stream stream,
        Action<XmltvChannel> onChannel,
        TimeSpan offset,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(onChannel);
        var body = await DecompressIfGzippedAsync(stream, cancellationToken).ConfigureAwait(false);
        await using var bodyScope = body.ConfigureAwait(false);
        var settings = new XmlReaderSettings
        {
            Async = true,
            DtdProcessing = DtdProcessing.Ignore,
            IgnoreComments = true,
            IgnoreWhitespace = true,
            XmlResolver = null,
        };
        using var reader = XmlReader.Create(body, settings);

        await reader.MoveToContentAsync().ConfigureAwait(false);
        while (!reader.EOF)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (reader.NodeType != XmlNodeType.Element || reader.Depth != 1)
            {
                await reader.ReadAsync().ConfigureAwait(false);
                continue;
            }

            var element = (XElement)await XNode.ReadFromAsync(reader, cancellationToken).ConfigureAwait(false);
            if (element.Name == "channel" && (string?)element.Attribute("id") is { Length: > 0 } id)
            {
                onChannel(new XmltvChannel(id, Text(element, "display-name"), Icon(element)));
            }
            else if (element.Name == "programme" && ToProgramme(element, offset) is { } programme)
            {
                yield return programme;
            }
        }
    }

    private static async Task<Stream> DecompressIfGzippedAsync(Stream stream, CancellationToken cancellationToken)
    {
        var pipe = PipeReader.Create(stream);
        var result = await pipe.ReadAtLeastAsync(2, cancellationToken).ConfigureAwait(false);
        var header = result.Buffer.Slice(0, Math.Min(2, result.Buffer.Length)).ToArray();
        pipe.AdvanceTo(result.Buffer.Start);

        var body = pipe.AsStream();
        return header is [0x1f, 0x8b] ? new GZipStream(body, CompressionMode.Decompress) : body;
    }

    private static XmltvProgramme? ToProgramme(XElement element, TimeSpan offset)
    {
        var channel = (string?)element.Attribute("channel");
        if (string.IsNullOrEmpty(channel) || ParseTime((string?)element.Attribute("start")) is not { } start)
        {
            return null;
        }

        var (season, episode) = ParseEpisode(element.Elements("episode-num")
            .FirstOrDefault(e => (string?)e.Attribute("system") == "xmltv_ns")?.Value);

        return new XmltvProgramme
        {
            ChannelId = channel,
            Start = start + offset,
            Stop = ParseTime((string?)element.Attribute("stop")) + offset,
            Title = Text(element, "title"),
            SubTitle = Text(element, "sub-title"),
            Description = Text(element, "desc"),
            Categories = element.Elements("category").Select(c => c.Value.Trim()).Where(c => c.Length > 0).ToList(),
            Icon = Icon(element),
            Season = season,
            Episode = episode,
            Year = Text(element, "date") is { Length: >= 4 } date && int.TryParse(date.AsSpan(0, 4), CultureInfo.InvariantCulture, out var year) ? year : null,
        };
    }

    private static string? Text(XElement element, string name)
        => element.Element(name)?.Value.Trim() is { Length: > 0 } value ? value : null;

    private static string? Icon(XElement element)
        => (string?)element.Element("icon")?.Attribute("src") is { Length: > 0 } src ? src : null;

    private static DateTimeOffset? ParseTime(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (!DateTime.TryParseExact(parts[0], TimeFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var local))
        {
            return null;
        }

        return new DateTimeOffset(local, parts.Length > 1 ? ParseZone(parts[1]) : TimeSpan.Zero);
    }

    private static TimeSpan ParseZone(string zone)
    {
        if (zone.Length != 5
            || !int.TryParse(zone.AsSpan(1, 2), CultureInfo.InvariantCulture, out var hours)
            || !int.TryParse(zone.AsSpan(3, 2), CultureInfo.InvariantCulture, out var minutes))
        {
            return TimeSpan.Zero;
        }

        var span = new TimeSpan(hours, minutes, 0);
        return zone[0] == '-' ? -span : span;
    }

    private static (int? Season, int? Episode) ParseEpisode(string? xmltvNs)
    {
        var parts = xmltvNs?.Split('.') ?? [];
        return (Part(parts, 0), Part(parts, 1));

        static int? Part(string[] parts, int index)
            => parts.Length > index && int.TryParse(parts[index].Split('/')[0], CultureInfo.InvariantCulture, out var zeroBased) ? zeroBased + 1 : null;
    }
}
