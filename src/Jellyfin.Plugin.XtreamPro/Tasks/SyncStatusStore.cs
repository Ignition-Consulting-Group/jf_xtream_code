using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Jellyfin.Plugin.XtreamPro.Sync;

namespace Jellyfin.Plugin.XtreamPro.Tasks;

/// <summary>
/// Remembers the last report of each sync task for the dashboard.
/// </summary>
public class SyncStatusStore
{
    private readonly string _path;
    private readonly ConcurrentDictionary<string, SyncStatus> _status;

    /// <summary>
    /// Initializes a new instance of the <see cref="SyncStatusStore"/> class.
    /// </summary>
    /// <param name="path">File the status is kept in.</param>
    public SyncStatusStore(string path)
    {
        _path = path;
        _status = new ConcurrentDictionary<string, SyncStatus>(
            File.Exists(path) ? JsonSerializer.Deserialize<Dictionary<string, SyncStatus>>(File.ReadAllText(path)) ?? [] : []);
    }

    /// <summary>
    /// Gets the last report per task key.
    /// </summary>
    public IReadOnlyDictionary<string, SyncStatus> All => _status;

    /// <summary>
    /// Records a finished sync.
    /// </summary>
    /// <param name="taskKey">Scheduled task key.</param>
    /// <param name="report">What the sync changed.</param>
    /// <param name="finishedAt">When it finished.</param>
    public void Record(string taskKey, SyncReport report, DateTimeOffset finishedAt)
    {
        _status[taskKey] = new SyncStatus(finishedAt, report);
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, JsonSerializer.Serialize(_status));
    }
}
