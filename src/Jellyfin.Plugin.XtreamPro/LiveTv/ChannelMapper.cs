using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Jellyfin.Plugin.XtreamPro.Api.Models;
using Jellyfin.Plugin.XtreamPro.Configuration;
using MediaBrowser.Controller.LiveTv;
using MediaBrowser.Model.LiveTv;

namespace Jellyfin.Plugin.XtreamPro.LiveTv;

/// <summary>
/// Turns provider live streams into Jellyfin channels.
/// </summary>
public static class ChannelMapper
{
    /// <summary>
    /// Maps provider channels, applying category rules, ordering and numbering.
    /// </summary>
    /// <param name="profile">Provider profile with the user's rules.</param>
    /// <param name="categories">Live categories in provider order.</param>
    /// <param name="channels">Live streams.</param>
    /// <param name="guideIcons">Channel icons from XMLTV, keyed by guide channel id.</param>
    /// <returns>Channels in display order.</returns>
    public static IReadOnlyList<ChannelInfo> Map(
        ProviderProfile profile,
        IEnumerable<Category> categories,
        IEnumerable<LiveChannel> channels,
        IReadOnlyDictionary<string, string>? guideIcons = null)
    {
        var rules = profile.LiveCategories.ToDictionary(r => r.CategoryId);
        var categoryOrder = categories
            .Select((c, index) => (c, Position: index))
            .OrderBy(x => x.c.CategoryId is not null && rules.TryGetValue(x.c.CategoryId, out var rule) && rule.Order is { } order ? order : int.MaxValue)
            .ThenBy(x => x.Position)
            .Select((x, index) => (x.c, index))
            .Where(x => x.c.CategoryId is not null)
            .ToDictionary(x => x.c.CategoryId!, x => (x.index, Name: rules.GetValueOrDefault(x.c.CategoryId!)?.Rename ?? x.c.CategoryName));
        var overrides = profile.ChannelNumbers.ToDictionary(o => o.StreamId, o => o.Number);

        var visible = channels
            .Where(c => c.CategoryId is null || !(rules.GetValueOrDefault(c.CategoryId)?.Excluded ?? false))
            .OrderBy(c => c.CategoryId is not null && categoryOrder.TryGetValue(c.CategoryId, out var cat) ? cat.index : int.MaxValue)
            .ThenBy(c => c.Num ?? int.MaxValue)
            .ToList();

        var nextNumber = visible.Max(c => c.Num) ?? 0;
        return visible.Select(c =>
        {
            var group = c.CategoryId is not null && categoryOrder.TryGetValue(c.CategoryId, out var cat) ? cat.Name : null;
            var image = c.StreamIcon ?? (c.EpgChannelId is not null ? guideIcons?.GetValueOrDefault(c.EpgChannelId) : null);
            return new ChannelInfo
            {
                Id = ChannelId.Format(profile.Id, c.StreamId),
                TunerHostId = ChannelId.TunerHostId,
                TunerChannelId = c.EpgChannelId,
                Name = c.Name,
                Number = overrides.GetValueOrDefault(c.StreamId) ?? (c.Num ?? ++nextNumber).ToString(CultureInfo.InvariantCulture),
                ChannelType = ChannelType.TV,
                ChannelGroup = group,
                Tags = group is null ? [] : [group],
                ImageUrl = image,
                HasImage = image is not null,
            };
        }).ToList();
    }
}
