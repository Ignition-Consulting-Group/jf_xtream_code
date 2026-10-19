using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;

namespace XtreamPro.IntegrationTests.FakeServer;

public class AccountInfoTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public AccountInfoTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task PlayerApi_WithValidCredentials_ReturnsActiveAccount()
    {
        using var client = _factory.CreateClient();

        var body = await client.GetFromJsonAsync<JsonElement>(
            "/player_api.php?username=demo&password=demo",
            TestContext.Current.CancellationToken);

        var user = body.GetProperty("user_info");
        user.GetProperty("auth").GetInt32().ShouldBe(1);
        user.GetProperty("status").GetString().ShouldBe("Active");
        user.GetProperty("max_connections").GetString().ShouldBe("1");
        body.GetProperty("server_info").GetProperty("url").GetString().ShouldBe("fake.xtream.test");
    }

    [Fact]
    public async Task PlayerApi_WithWrongPassword_ReturnsAuthZeroLikeRealProviders()
    {
        using var client = _factory.CreateClient();

        var body = await client.GetFromJsonAsync<JsonElement>(
            "/player_api.php?username=demo&password=nope",
            TestContext.Current.CancellationToken);

        body.GetProperty("user_info").GetProperty("auth").GetInt32().ShouldBe(0);
    }
}
