using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Jellyfin.Plugin.XtreamPro.Api;
using Polly.CircuitBreaker;

namespace XtreamPro.UnitTests.Api;

public class ResilienceHandlerTests
{
    private static readonly XtreamResilienceOptions Fast = new()
    {
        RetryCount = 2,
        BaseDelay = TimeSpan.FromMilliseconds(1),
        AttemptTimeout = TimeSpan.FromSeconds(5),
        BreakDuration = TimeSpan.FromMinutes(1),
        MinimumThroughput = 3,
    };

    private static HttpClient Client(StubHandler stub)
        => new(new ResilienceHandler(new XtreamResilience(Fast)) { InnerHandler = stub });

    [Fact]
    public async Task TransientServerError_IsRetried()
    {
        var calls = 0;
        var stub = new StubHandler(_ => ++calls < 3 ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) : StubHandler.Json("{}"));

        using var response = await Client(stub).GetAsync(new Uri("http://a.fake.test/"), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        calls.ShouldBe(3);
    }

    [Fact]
    public async Task ClientError_IsNotRetried()
    {
        var stub = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.Forbidden));

        using var response = await Client(stub).GetAsync(new Uri("http://a.fake.test/"), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        stub.Requests.Count.ShouldBe(1);
    }

    [Fact]
    public async Task RepeatedFailures_OpenTheCircuitForThatProviderOnly()
    {
        var stub = new StubHandler(r => r.RequestUri!.Host == "down.fake.test"
            ? new HttpResponseMessage(HttpStatusCode.BadGateway)
            : StubHandler.Json("{}"));
        var client = Client(stub);
        var ct = TestContext.Current.CancellationToken;

        for (var i = 0; i < 3; i++)
        {
            try
            {
                using var _ = await client.GetAsync(new Uri("http://down.fake.test/"), ct);
            }
            catch (BrokenCircuitException)
            {
                break;
            }
        }

        await Should.ThrowAsync<BrokenCircuitException>(() => client.GetAsync(new Uri("http://down.fake.test/"), ct));
        using var healthy = await client.GetAsync(new Uri("http://up.fake.test/"), ct);
        healthy.StatusCode.ShouldBe(HttpStatusCode.OK);
        stub.Requests.Count(r => r.RequestUri!.Host == "up.fake.test").ShouldBe(1);
    }

    [Theory]
    [InlineData("http://iptv.fake.test/xmltv.php?username=u&password=p")]
    [InlineData("http://iptv.fake.test/player_api.php?username=u&password=p&action=get_short_epg&stream_id=1")]
    public async Task FailingGuideRequests_DoNotBlockTheRestOfTheProvider(string guideUrl)
    {
        var stub = new StubHandler(r => r.RequestUri!.Query.Contains("get_live_streams", StringComparison.Ordinal)
            ? StubHandler.Json("[]")
            : new HttpResponseMessage(HttpStatusCode.BadGateway));
        var client = Client(stub);
        var ct = TestContext.Current.CancellationToken;

        for (var i = 0; i < 3; i++)
        {
            try
            {
                using var _ = await client.GetAsync(new Uri(guideUrl), ct);
            }
            catch (BrokenCircuitException)
            {
                break;
            }
        }

        await Should.ThrowAsync<BrokenCircuitException>(() => client.GetAsync(new Uri(guideUrl), ct));
        using var channels = await client.GetAsync(new Uri("http://iptv.fake.test/player_api.php?username=u&password=p&action=get_live_streams"), ct);
        channels.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
