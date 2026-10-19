using System;
using System.Linq;
using Jellyfin.Plugin.XtreamPro.Api.Models;
using Jellyfin.Plugin.XtreamPro.Configuration;
using Jellyfin.Plugin.XtreamPro.LiveTv;

namespace XtreamPro.UnitTests.LiveTv;

public class ChannelMapperTests
{
    private static readonly Category[] Categories =
    [
        new() { CategoryId = "2", CategoryName = "Sports" },
        new() { CategoryId = "1", CategoryName = "UK | News" },
    ];

    private readonly ProviderProfile _profile = new() { Id = Guid.Parse("11111111-2222-3333-4444-555555555555"), Url = "http://fake.xtream.test" };

    private static LiveChannel Channel(int id, string category, int? num = null, string? icon = null)
        => new() { StreamId = id, Name = "Ch " + id, CategoryId = category, Num = num, StreamIcon = icon, EpgChannelId = "epg." + id };

    [Fact]
    public void Map_OrdersByCategoryThenProviderNumber()
    {
        var channels = ChannelMapper.Map(_profile, Categories, [Channel(10, "1", 5), Channel(11, "2", 9), Channel(12, "2", 3)]);

        channels.Select(c => c.Name).ShouldBe(["Ch 12", "Ch 11", "Ch 10"]);
    }

    [Fact]
    public void Map_UserCategoryOrderComesFirst()
    {
        _profile.LiveCategories.Add(new CategoryRule { CategoryId = "1", Order = 0 });

        var channels = ChannelMapper.Map(_profile, Categories, [Channel(10, "2", 1), Channel(11, "1", 2)]);

        channels.Select(c => c.Name).ShouldBe(["Ch 11", "Ch 10"]);
    }

    [Fact]
    public void Map_UsesProviderNumberAndUserOverrides()
    {
        _profile.ChannelNumbers.Add(new ChannelNumberOverride { StreamId = 11, Number = "501" });

        var channels = ChannelMapper.Map(_profile, Categories, [Channel(10, "1", 5), Channel(11, "1", 6)]);

        channels.Select(c => c.Number).ShouldBe(["5", "501"]);
    }

    [Fact]
    public void Map_NumbersChannelsWithoutProviderNumberAfterTheHighest()
    {
        var channels = ChannelMapper.Map(_profile, Categories, [Channel(10, "1", 40), Channel(11, "1"), Channel(12, "1")]);

        channels.Select(c => c.Number).ShouldBe(["40", "41", "42"]);
    }

    [Fact]
    public void Map_DropsExcludedCategoriesAndAppliesRenames()
    {
        _profile.LiveCategories.Add(new CategoryRule { CategoryId = "2", Excluded = true });
        _profile.LiveCategories.Add(new CategoryRule { CategoryId = "1", Rename = "News" });

        var channel = ChannelMapper.Map(_profile, Categories, [Channel(10, "1"), Channel(11, "2")]).ShouldHaveSingleItem();

        channel.ChannelGroup.ShouldBe("News");
        channel.Tags.ShouldBe(["News"]);
    }

    [Fact]
    public void Map_SetsIdsLogoAndGuideId()
    {
        var channel = ChannelMapper.Map(_profile, Categories, [Channel(10, "1", 1, "http://fake.xtream.test/logo.png")]).Single();

        channel.Id.ShouldBe(ChannelId.Format(_profile.Id, 10));
        channel.TunerHostId.ShouldBe(ChannelId.TunerHostId);
        channel.TunerChannelId.ShouldBe("epg.10");
        channel.ImageUrl.ShouldBe("http://fake.xtream.test/logo.png");
        channel.HasImage.ShouldBe(true);
    }

    [Fact]
    public void Map_WithoutLogo_FallsBackToGuideIcon()
    {
        var icons = new System.Collections.Generic.Dictionary<string, string> { ["epg.10"] = "http://fake.xtream.test/guide-icon.png" };

        var channel = ChannelMapper.Map(_profile, Categories, [Channel(10, "1")], icons).Single();

        channel.ImageUrl.ShouldBe("http://fake.xtream.test/guide-icon.png");
    }

    [Fact]
    public void Map_WithNoLogoAnywhere_HasNoImage()
    {
        ChannelMapper.Map(_profile, Categories, [Channel(10, "1")]).Single().HasImage.ShouldBe(false);
    }

    [Fact]
    public void Map_ChannelInUnknownCategory_SortsLast()
    {
        var channels = ChannelMapper.Map(_profile, Categories, [Channel(10, "99", 1), Channel(11, "1", 2)]);

        channels.Select(c => c.Name).ShouldBe(["Ch 11", "Ch 10"]);
        channels[1].ChannelGroup.ShouldBeNull();
    }
}
