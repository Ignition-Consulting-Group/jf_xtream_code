namespace Jellyfin.Plugin.XtreamPro.Sync;

/// <summary>
/// What a sync changed.
/// </summary>
/// <param name="Added">Items written for the first time.</param>
/// <param name="Updated">Items with at least one changed file.</param>
/// <param name="Removed">Items deleted because the provider no longer has them.</param>
/// <param name="Unchanged">Items left as they were.</param>
public sealed record SyncReport(int Added, int Updated, int Removed, int Unchanged);
