using System;
using System.Threading.Tasks;
using Jellyfin.Plugin.XtreamPro.Api;
using Microsoft.AspNetCore.Mvc.Testing;

namespace XtreamPro.IntegrationTests.FakeServer;

public class XtreamClientTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public XtreamClientTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetAccountInfo_AgainstFakeServer()
    {
        var client = new XtreamClient(_factory.CreateClient(), new XtreamCredentials(_factory.Server.BaseAddress, "demo", "demo"), "Test/1.0");

        var account = await client.GetAccountInfoAsync(TestContext.Current.CancellationToken);

        account.UserInfo.Auth.ShouldBeTrue();
        account.UserInfo.MaxConnections.ShouldBe(1);
        account.UserInfo.ExpiresAt.ShouldNotBeNull().ShouldBeGreaterThan(DateTimeOffset.UtcNow);
        account.ServerInfo!.Host.ShouldBe("fake.xtream.test");
    }
}
