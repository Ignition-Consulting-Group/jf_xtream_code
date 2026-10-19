using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace Jellyfin.Plugin.XtreamPro.Sync;

/// <summary>
/// Strips provider prefixes and quality tags from titles and pulls out the year.
/// </summary>
public class TitleCleaner
{
    /// <summary>
    /// Rules applied when the user has not set their own.
    /// </summary>
    public static readonly IReadOnlyList<string> DefaultRules =
    [
        @"^\s*\|?[A-Z]{2,3}\|?\s*[|:\-]\s*",
        @"^\s*(4K|UHD|FHD|HD|SD)\s*[|:\-]\s*",
        @"\b(2160p|1080p|720p|480p|HEVC|x265|x264|H\.?264|H\.?265|UHD|4K|HDR10?|10bit|MULTI)\b",
    ];

    private static readonly TimeSpan Timeout = TimeSpan.FromMilliseconds(100);
    private static readonly Regex YearPattern = new(@"(?:\s*[(\[](?<year>(?:19|20)\d{2})[)\]]|\s+-\s+(?<year>(?:19|20)\d{2}))\s*$", RegexOptions.None, Timeout);
    private static readonly Regex Whitespace = new(@"\s+", RegexOptions.None, Timeout);
    private static readonly char[] TrimChars = [' ', '-', '|', ':', '.'];

    private readonly Regex[] _rules;

    /// <summary>
    /// Initializes a new instance of the <see cref="TitleCleaner"/> class.
    /// </summary>
    /// <param name="rules">Regular expressions whose matches are removed, in order. Invalid patterns are skipped.</param>
    public TitleCleaner(IEnumerable<string> rules)
    {
        _rules = rules.Select(TryCreate).OfType<Regex>().ToArray();
    }

    /// <summary>
    /// Cleans a provider title.
    /// </summary>
    /// <param name="raw">Title as sent by the provider.</param>
    /// <returns>The clean title and the year if one was found.</returns>
    public (string Title, int? Year) Clean(string raw)
    {
        ArgumentNullException.ThrowIfNull(raw);
        var title = _rules.Aggregate(raw, (current, rule) => rule.Replace(current, " "));

        int? year = null;
        var match = YearPattern.Match(title);
        if (match.Success && match.Index > 0)
        {
            year = int.Parse(match.Groups["year"].Value, CultureInfo.InvariantCulture);
            title = title[..match.Index];
        }

        title = Whitespace.Replace(title, " ").Trim(TrimChars);
        return (title.Length > 0 ? title : raw.Trim(), year);
    }

    private static Regex? TryCreate(string pattern)
    {
        try
        {
            return new Regex(pattern, RegexOptions.IgnoreCase, Timeout);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }
}
