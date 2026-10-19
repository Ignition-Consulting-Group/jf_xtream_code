using System;
using System.Net.Http;
using System.Web;
using Jellyfin.Plugin.XtreamPro.Api;
using Jellyfin.Plugin.XtreamPro.Configuration;
using NSubstitute;

namespace XtreamPro.UnitTests;

/// <summary>
/// An Xtream provider backed by fixtures, routed by action.
/// </summary>
internal sealed class FakeProvider
{
    public FakeProvider()
    {
        Handler = new StubHandler(r => StubHandler.Json(Fixture.Read(FixtureFor(r.RequestUri!))));
        Clients = ClientsFor(Handler);
        Settings = Substitute.For<IXtreamSettings>();
        Settings.Current.Returns(Configuration);
        Configuration.Providers.Add(Profile);
    }

    public ProviderProfile Profile { get; } = new() { Id = Guid.Parse("11111111-2222-3333-4444-555555555555"), Name = "Fake", Url = "http://fake.xtream.test", Username = "demo", Password = "demo" };

    public PluginConfiguration Configuration { get; } = new();

    public StubHandler Handler { get; }

    public XtreamClientFactory Clients { get; }

    public IXtreamSettings Settings { get; }

    public static XtreamClientFactory ClientsFor(StubHandler handler)
    {
        var httpFactory = Substitute.For<IHttpClientFactory>();
        httpFactory.CreateClient(Arg.Any<string>()).Returns(_ => new HttpClient(handler));
        return new XtreamClientFactory(httpFactory);
    }

    private static string FixtureFor(Uri uri)
    {
        if (uri.AbsolutePath == "/xmltv.php")
        {
            return "guide.xml";
        }

        return HttpUtility.ParseQueryString(uri.Query)["action"] switch
        {
            null => "account_info.json",
            "get_live_categories" => "live_categories.json",
            "get_live_streams" => "live_streams.json",
            "get_short_epg" => "short_epg.json",
            "get_vod_categories" => "vod_categories.json",
            "get_vod_streams" => "vod_streams.json",
            "get_series_categories" => "series_categories.json",
            "get_series" => "series.json",
            "get_series_info" => "series_info.json",
            var action => throw new InvalidOperationException("No fixture for " + action),
        };
    }
}
