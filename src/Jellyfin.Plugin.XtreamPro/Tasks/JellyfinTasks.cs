using System.Linq;
using MediaBrowser.Model.Tasks;

namespace Jellyfin.Plugin.XtreamPro.Tasks;

/// <summary>
/// Helpers for Jellyfin's built-in scheduled tasks.
/// </summary>
internal static class JellyfinTasks
{
    /// <summary>
    /// Queues Jellyfin's guide refresh, which re-reads channels and programmes from all tuners and guide providers.
    /// </summary>
    /// <param name="tasks">Task manager.</param>
    public static void QueueGuideRefresh(ITaskManager tasks)
    {
        if (tasks.ScheduledTasks.FirstOrDefault(t => t.ScheduledTask.Key == "RefreshGuide") is { } worker)
        {
            tasks.QueueScheduledTask(worker.ScheduledTask, new TaskOptions());
        }
    }
}
