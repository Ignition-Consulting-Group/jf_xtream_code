namespace Jellyfin.Plugin.XtreamPro.Api.Models;

/// <summary>
/// A live, VOD or series category.
/// </summary>
public sealed record Category
{
    /// <summary>Gets the category id.</summary>
    public string? CategoryId { get; init; }

    /// <summary>Gets the category name.</summary>
    public string? CategoryName { get; init; }

    /// <summary>Gets the parent category id.</summary>
    public int? ParentId { get; init; }
}
