using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Jellyfin.Plugin.XtreamPro.LiveTv;
using Jellyfin.Plugin.XtreamPro.LiveTv.Xmltv;

namespace XtreamPro.UnitTests.LiveTv;

public sealed class GuideStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "xtreampro-tests", Guid.NewGuid().ToString("N"));
    private readonly Guid _provider = Guid.NewGuid();

    private static XmltvProgramme At(int hour)
        => new() { ChannelId = "x", Title = "P" + hour, Start = new DateTimeOffset(2026, 3, 4, hour, 0, 0, TimeSpan.Zero), Stop = new DateTimeOffset(2026, 3, 4, hour + 1, 0, 0, TimeSpan.Zero) };

    private static GuideResult Guide(params XmltvProgramme[] programmes)
        => new(new Dictionary<string, IReadOnlyList<XmltvProgramme>> { ["ch1"] = programmes }, new Dictionary<string, string>(), ["ch2"]);

    [Fact]
    public async Task Get_ReturnsProgrammesOverlappingTheRange()
    {
        var store = new GuideStore(_dir);
        await store.SaveAsync(_provider, Guide(At(1), At(2), At(3)), TestContext.Current.CancellationToken);

        var programmes = await store.GetAsync(_provider, "ch1", new DateTime(2026, 3, 4, 2, 30, 0, DateTimeKind.Utc), new DateTime(2026, 3, 4, 3, 30, 0, DateTimeKind.Utc), TestContext.Current.CancellationToken);

        programmes.Select(p => p.Title).ShouldBe(["P2", "P3"]);
    }

    [Fact]
    public async Task Guide_SurvivesARestart()
    {
        await new GuideStore(_dir).SaveAsync(_provider, Guide(At(1)), TestContext.Current.CancellationToken);

        var reloaded = new GuideStore(_dir);

        (await reloaded.GetAsync(_provider, "ch1", DateTime.MinValue, DateTime.MaxValue, TestContext.Current.CancellationToken)).ShouldHaveSingleItem();
        (await reloaded.LoadAsync(_provider, TestContext.Current.CancellationToken))!.ChannelsWithoutGuide.ShouldBe(["ch2"]);
    }

    [Fact]
    public async Task Get_UnknownProviderOrChannel_IsEmpty()
    {
        var store = new GuideStore(_dir);

        (await store.GetAsync(_provider, "ch1", DateTime.MinValue, DateTime.MaxValue, TestContext.Current.CancellationToken)).ShouldBeEmpty();
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, true);
        }
    }
}
