using System;
using System.Threading.Tasks;
using Jellyfin.Plugin.XtreamPro.Api;
using Microsoft.Extensions.Time.Testing;

namespace XtreamPro.UnitTests.Api;

public class AccountMonitorTests
{
    private readonly FakeProvider _provider = new();
    private readonly FakeTimeProvider _time = new();
    private readonly AccountMonitor _monitor;

    public AccountMonitorTests()
    {
        _monitor = new AccountMonitor(_provider.Clients, _time);
    }

    [Fact]
    public async Task Get_CachesWithinMaxAge()
    {
        await _monitor.GetAsync(_provider.Profile, TimeSpan.FromMinutes(5), TestContext.Current.CancellationToken);
        _time.Advance(TimeSpan.FromMinutes(4));

        var account = await _monitor.GetAsync(_provider.Profile, TimeSpan.FromMinutes(5), TestContext.Current.CancellationToken);

        account.UserInfo.MaxConnections.ShouldBe(2);
        _provider.Handler.Requests.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Get_RefreshesWhenStale()
    {
        await _monitor.GetAsync(_provider.Profile, TimeSpan.FromMinutes(5), TestContext.Current.CancellationToken);
        _time.Advance(TimeSpan.FromMinutes(6));

        await _monitor.GetAsync(_provider.Profile, TimeSpan.FromMinutes(5), TestContext.Current.CancellationToken);

        _provider.Handler.Requests.Count.ShouldBe(2);
    }

    [Fact]
    public async Task Last_ReturnsTheMostRecentResult()
    {
        _monitor.Last(_provider.Profile.Id).ShouldBeNull();

        await _monitor.GetAsync(_provider.Profile, TimeSpan.Zero, TestContext.Current.CancellationToken);

        _monitor.Last(_provider.Profile.Id)!.UserInfo.Status.ShouldBe("Active");
    }
}
