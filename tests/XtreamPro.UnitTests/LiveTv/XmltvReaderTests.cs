using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using Jellyfin.Plugin.XtreamPro.LiveTv.Xmltv;

namespace XtreamPro.UnitTests.LiveTv;

public class XmltvReaderTests
{
    private static async Task<(List<XmltvChannel> Channels, List<XmltvProgramme> Programmes)> Read(Stream stream, TimeSpan offset = default)
    {
        var channels = new List<XmltvChannel>();
        var programmes = new List<XmltvProgramme>();
        await foreach (var p in XmltvReader.ReadAsync(stream, channels.Add, offset, TestContext.Current.CancellationToken))
        {
            programmes.Add(p);
        }

        return (channels, programmes);
    }

    private static Task<(List<XmltvChannel> Channels, List<XmltvProgramme> Programmes)> ReadFixture(TimeSpan offset = default)
        => Read(File.OpenRead(Fixture.Path("guide.xml")), offset);

    [Fact]
    public async Task Channels_IncludeIconsWhenPresent()
    {
        var (channels, _) = await ReadFixture();

        channels.ShouldBe([
            new XmltvChannel("news.one.uk", "News One", "http://fake.xtream.test/guide/news.png"),
            new XmltvChannel("sports.two", "Sports Two", null),
        ]);
    }

    [Fact]
    public async Task Programme_ReadsAllFields()
    {
        var (_, programmes) = await ReadFixture();

        programmes[0].ShouldSatisfyAllConditions(
            p => p.ChannelId.ShouldBe("news.one.uk"),
            p => p.Start.ShouldBe(new DateTimeOffset(2026, 3, 4, 7, 0, 0, TimeSpan.Zero)),
            p => p.Stop.ShouldBe(new DateTimeOffset(2026, 3, 4, 8, 0, 0, TimeSpan.Zero)),
            p => p.Title.ShouldBe("Morning News & Weather"),
            p => p.SubTitle.ShouldBe("Tuesday"),
            p => p.Description.ShouldBe("The morning bulletin."),
            p => p.Categories.ShouldBe(["News", "Weather"]),
            p => p.Season.ShouldBe(3),
            p => p.Episode.ShouldBe(5),
            p => p.Icon.ShouldBe("http://fake.xtream.test/guide/p1.jpg"),
            p => p.Year.ShouldBe(2026));
    }

    [Fact]
    public async Task Times_HonourTimezoneAndTreatMissingTimezoneAsUtc()
    {
        var (_, programmes) = await ReadFixture();

        programmes.Single(p => p.Title == "With Offset").Start.UtcDateTime.ShouldBe(new DateTime(2026, 3, 4, 7, 0, 0, DateTimeKind.Utc));
        programmes.Single(p => p.Title == "No Timezone").Start.ShouldBe(new DateTimeOffset(2026, 3, 4, 9, 0, 0, TimeSpan.Zero));
        programmes.Single(p => p.Title == "Short Time Format").Start.ShouldBe(new DateTimeOffset(2026, 3, 4, 11, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public async Task MissingStop_IsNull_AndBadStart_IsSkipped()
    {
        var (_, programmes) = await ReadFixture();

        programmes.Single(p => p.Title == "No Stop").Stop.ShouldBeNull();
        programmes.ShouldNotContain(p => p.Title == "Bad Start");
        programmes.Count.ShouldBe(5);
    }

    [Fact]
    public async Task Offset_ShiftsAllTimes()
    {
        var (_, programmes) = await ReadFixture(TimeSpan.FromMinutes(-60));

        programmes[0].Start.ShouldBe(new DateTimeOffset(2026, 3, 4, 6, 0, 0, TimeSpan.Zero));
        programmes[0].Stop.ShouldBe(new DateTimeOffset(2026, 3, 4, 7, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public async Task GzippedBodyWithoutHeader_IsDetected()
    {
        var gz = new MemoryStream();
        await using (var zip = new GZipStream(gz, CompressionLevel.Fastest, leaveOpen: true))
        await using (var file = File.OpenRead(Fixture.Path("guide.xml")))
        {
            await file.CopyToAsync(zip, TestContext.Current.CancellationToken);
        }

        gz.Position = 0;
        var (channels, programmes) = await Read(new NonSeekableStream(gz));

        channels.Count.ShouldBe(2);
        programmes.Count.ShouldBe(5);
    }

    private sealed class NonSeekableStream(Stream inner) : Stream
    {
        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
