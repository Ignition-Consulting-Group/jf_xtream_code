using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Jellyfin.Plugin.XtreamPro.Api;
using Jellyfin.Plugin.XtreamPro.Configuration;
using NSubstitute;

namespace XtreamPro.UnitTests.Api;

public class XtreamClientFactoryTests
{
    private readonly StubHandler _stub = StubHandler.ForFixture("account_info.json");
    private readonly XtreamClientFactory _factory;

    public XtreamClientFactoryTests()
    {
        var httpFactory = Substitute.For<IHttpClientFactory>();
        httpFactory.CreateClient(XtreamClientFactory.HttpClientName).Returns(_ => new HttpClient(_stub));
        _factory = new XtreamClientFactory(httpFactory);
    }

    [Fact]
    public async Task Create_UsesProfileUrlAndCredentials()
    {
        var profile = new ProviderProfile { Url = "http://fake.xtream.test:8080", Username = "demo", Password = "secret" };

        await _factory.Create(profile).GetAccountInfoAsync(TestContext.Current.CancellationToken);

        _stub.Requests.Single().RequestUri.ShouldBe(new Uri("http://fake.xtream.test:8080/player_api.php?username=demo&password=secret"));
    }

    [Fact]
    public async Task Create_FallsBackToTheDefaultUserAgent()
    {
        var profile = new ProviderProfile { Url = "http://fake.xtream.test", Username = "u", Password = "p", UserAgent = " " };

        await _factory.Create(profile).GetAccountInfoAsync(TestContext.Current.CancellationToken);

        _stub.Requests.Single().Headers.UserAgent.ToString().ShouldBe(ProviderProfile.DefaultUserAgent);
    }

    [Fact]
    public void Create_RejectsAProfileWithoutAValidUrl()
    {
        Should.Throw<ArgumentException>(() => _factory.Create(new ProviderProfile { Url = "not a url" }));
    }
}
