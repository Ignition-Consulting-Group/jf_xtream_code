namespace Jellyfin.Plugin.XtreamPro.Web;

/// <summary>
/// A provider category for the category manager.
/// </summary>
/// <param name="Id">Category id.</param>
/// <param name="Name">Category name.</param>
public sealed record CategoryOption(string Id, string Name);
