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
        var httpFactory = Substitute.For<IHttpClientFactory>();
        httpFactory.CreateClient(Arg.Any<string>()).Returns(_ => new HttpClient(Handler));
        Clients = new XtreamClientFactory(httpFactory);
        Settings = Substitute.For<IXtreamSettings>();
        Settings.Current.Returns(Configuration);
        Configuration.Providers.Add(Profile);
    }

    public ProviderProfile Profile { get; } = new() { Id = Guid.Parse("11111111-2222-3333-4444-555555555555"), Name = "Fake", Url = "http://fake.xtream.test", Username = "demo", Password = "demo" };

    public PluginConfiguration Configuration { get; } = new();

    public StubHandler Handler { get; }

    public XtreamClientFactory Clients { get; }

    public IXtreamSettings Settings { get; }

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
            var action => throw new InvalidOperationException("No fixture for " + action),
        };
    }
}
