using System;
using System.IO;
using Jellyfin.Plugin.XtreamPro.Api;
using Jellyfin.Plugin.XtreamPro.LiveTv;
using Microsoft.Extensions.Logging.Abstractions;

namespace XtreamPro.UnitTests.LiveTv;

internal sealed class LiveTvHarness
{
    public LiveTvHarness()
    {
        Guides = new GuideStore(Path.Combine(Path.GetTempPath(), "xtreampro-tests", Guid.NewGuid().ToString("N")));
        Catalog = new LiveCatalog(Provider.Settings, Provider.Clients, Guides, NullLogger<LiveCatalog>.Instance);
        Accounts = new AccountMonitor(Provider.Clients, TimeProvider.System);
    }

    public FakeProvider Provider { get; } = new();

    public GuideStore Guides { get; }

    public LiveCatalog Catalog { get; }

    public AccountMonitor Accounts { get; }

    public ConnectionTracker Connections { get; } = new();

    public XtreamTunerHost TunerHost() => new(Catalog, Accounts, Connections);

    public string Channel(int streamId) => ChannelId.Format(Provider.Profile.Id, streamId);
}
