namespace Jellyfin.Plugin.XtreamPro.Configuration;

/// <summary>
/// User choice for one provider category.
/// </summary>
public class CategoryRule
{
    /// <summary>Gets or sets the provider category id.</summary>
    public string CategoryId { get; set; } = string.Empty;

    /// <summary>Gets or sets a value indicating whether the category is hidden.</summary>
    public bool Excluded { get; set; }

    /// <summary>Gets or sets the display name override.</summary>
    public string? Rename { get; set; }
}
