using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Jellyfin.Plugin.XtreamPro.Api;
using MediaBrowser.Controller.LiveTv;
using MediaBrowser.Model.MediaInfo;

namespace XtreamPro.UnitTests.LiveTv;

public class XtreamTunerHostTests
{
    private readonly LiveTvHarness _h = new();

    [Fact]
    public async Task GetChannels_ReturnsMappedChannels()
    {
        (await _h.TunerHost().GetChannels(false, TestContext.Current.CancellationToken)).Count.ShouldBe(3);
    }

    [Fact]
    public async Task MediaSources_PointAtTheProviderStream()
    {
        var source = (await _h.TunerHost().GetChannelStreamMediaSources(_h.Channel(101), TestContext.Current.CancellationToken)).ShouldHaveSingleItem();

        source.ShouldSatisfyAllConditions(
            s => s.Path.ShouldBe("http://fake.xtream.test/live/demo/demo/101.ts"),
            s => s.Protocol.ShouldBe(MediaProtocol.Http),
            s => s.Container.ShouldBe("ts"),
            s => s.IsInfiniteStream.ShouldBeTrue(),
            s => s.RequiresOpening.ShouldBeTrue(),
            s => s.RequiresClosing.ShouldBeTrue(),
            s => s.SupportsDirectPlay.ShouldBeTrue(),
            s => s.RequiredHttpHeaders["User-Agent"].ShouldBe(_h.Provider.Profile.UserAgent));
    }

    [Fact]
    public async Task MediaSources_Hls_IsNotDirectPlayed()
    {
        _h.Provider.Profile.LiveFormat = LiveFormat.Hls;

        var source = (await _h.TunerHost().GetChannelStreamMediaSources(_h.Channel(101), TestContext.Current.CancellationToken)).Single();

        source.Path.ShouldEndWith("101.m3u8");
        source.SupportsDirectPlay.ShouldBeFalse();
        source.SupportsDirectStream.ShouldBeTrue();
    }

    [Fact]
    public async Task MediaSources_DirectPlayOff_IsRespected()
    {
        _h.Provider.Profile.PreferDirectPlay = false;

        (await _h.TunerHost().GetChannelStreamMediaSources(_h.Channel(101), TestContext.Current.CancellationToken)).Single().SupportsDirectPlay.ShouldBeFalse();
    }

    [Fact]
    public async Task MediaSources_ForOtherTuners_AreEmpty()
    {
        (await _h.TunerHost().GetChannelStreamMediaSources("m3u_abc", TestContext.Current.CancellationToken)).ShouldBeEmpty();
    }

    [Fact]
    public async Task GetChannelStream_ForOtherTuners_ThrowsFileNotFound()
    {
        await Should.ThrowAsync<FileNotFoundException>(() => _h.TunerHost().GetChannelStream("m3u_abc", "s", [], TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetChannelStream_TakesAConnectionUntilClosed()
    {
        var stream = await _h.TunerHost().GetChannelStream(_h.Channel(101), "s1", [], TestContext.Current.CancellationToken);

        _h.Connections.Active(_h.Provider.Profile.Id).ShouldBe(1);
        stream.MediaSource.Path.ShouldEndWith("/101.ts");
        stream.TunerHostId.ShouldBe("xtreampro");
        stream.EnableStreamSharing.ShouldBeFalse();

        await stream.Close();
        _h.Connections.Active(_h.Provider.Profile.Id).ShouldBe(0);
    }

    [Fact]
    public async Task GetChannelStream_OverTheAccountLimit_IsAConflict()
    {
        var host = _h.TunerHost();
        await host.GetChannelStream(_h.Channel(101), "a", [], TestContext.Current.CancellationToken);
        await host.GetChannelStream(_h.Channel(102), "b", [], TestContext.Current.CancellationToken);

        await Should.ThrowAsync<LiveTvConflictException>(() => host.GetChannelStream(_h.Channel(103), "c", [], TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task LiveStream_DoesNotExposeALocalStream()
    {
        using var stream = await _h.TunerHost().GetChannelStream(_h.Channel(101), "s", [], TestContext.Current.CancellationToken);

        Should.Throw<NotSupportedException>(() => stream.GetStream());
    }
}
