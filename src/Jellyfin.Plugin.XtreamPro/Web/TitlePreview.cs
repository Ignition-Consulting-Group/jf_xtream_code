namespace Jellyfin.Plugin.XtreamPro.Web;

/// <summary>
/// A provider title and what the cleanup rules make of it.
/// </summary>
/// <param name="Raw">Title as sent by the provider.</param>
/// <param name="Title">Clean title.</param>
/// <param name="Year">Year found in the title.</param>
public sealed record TitlePreview(string Raw, string Title, int? Year);
