using System;
using Jellyfin.Plugin.XtreamPro.Sync;

namespace XtreamPro.UnitTests.Sync;

public class StreamUrlsTests
{
    private static readonly Guid Provider = Guid.Parse("11111111-2222-3333-4444-555555555555");
    private readonly StreamUrls _urls = new("http://jellyfin.local:8096/", "c2VjcmV0LXNlY3JldC1zZWNyZXQtc2VjcmV0");

    [Fact]
    public void For_BuildsAPluginUrlWithoutCredentials()
    {
        var url = _urls.For(Provider, StreamKind.Movie, 201, "mkv");

        url.ShouldStartWith("http://jellyfin.local:8096/XtreamPro/Stream/11111111222233334444555555555555/movie/201.mkv?sig=");
        url.ShouldNotContain("demo");
    }

    [Fact]
    public void Verify_AcceptsItsOwnSignature()
    {
        var sig = new Uri(_urls.For(Provider, StreamKind.Episode, 4001, "mp4")).Query["?sig=".Length..];

        _urls.Verify(Provider, StreamKind.Episode, 4001, sig).ShouldBeTrue();
    }

    [Fact]
    public void Verify_RejectsTamperedIdsAndOtherSecrets()
    {
        var sig = new Uri(_urls.For(Provider, StreamKind.Movie, 201, "mkv")).Query["?sig=".Length..];

        _urls.Verify(Provider, StreamKind.Movie, 202, sig).ShouldBeFalse();
        _urls.Verify(Provider, StreamKind.Episode, 201, sig).ShouldBeFalse();
        new StreamUrls("http://x", "b3RoZXItb3RoZXItb3RoZXItb3RoZXI=").Verify(Provider, StreamKind.Movie, 201, sig).ShouldBeFalse();
        _urls.Verify(Provider, StreamKind.Movie, 201, null).ShouldBeFalse();
        _urls.Verify(Provider, StreamKind.Movie, 201, "!!").ShouldBeFalse();
    }

    [Fact]
    public void NewSecret_IsRandom()
    {
        StreamUrls.NewSecret().ShouldNotBe(StreamUrls.NewSecret());
    }
}
