using System.Collections.Generic;

namespace Jellyfin.Plugin.XtreamPro.Sync;

/// <summary>
/// The files one provider item should have in the library.
/// </summary>
/// <param name="Key">Stable key, for example <c>movie:201</c>.</param>
/// <param name="Files">File contents keyed by path relative to the library folder, using <c>/</c> separators.</param>
public sealed record SyncItem(string Key, IReadOnlyDictionary<string, string> Files);
