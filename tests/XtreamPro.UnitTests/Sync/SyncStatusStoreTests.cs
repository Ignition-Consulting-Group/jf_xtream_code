using System;
using System.IO;
using Jellyfin.Plugin.XtreamPro.Sync;
using Jellyfin.Plugin.XtreamPro.Tasks;

namespace XtreamPro.UnitTests.Sync;

public class SyncStatusStoreTests
{
    [Fact]
    public void Record_SurvivesARestart()
    {
        var path = Path.Combine(Path.GetTempPath(), "xtreampro-tests", Guid.NewGuid().ToString("N"), "status.json");
        var at = new DateTimeOffset(2026, 3, 4, 1, 2, 3, TimeSpan.Zero);

        new SyncStatusStore(path).Record("XtreamProSyncMovies", new SyncReport(1, 2, 3, 4), at);

        new SyncStatusStore(path).All["XtreamProSyncMovies"].ShouldBe(new SyncStatus(at, new SyncReport(1, 2, 3, 4)));
    }
}
