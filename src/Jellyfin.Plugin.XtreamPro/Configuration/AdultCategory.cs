using System.Text.RegularExpressions;

namespace Jellyfin.Plugin.XtreamPro.Configuration;

/// <summary>
/// Recognises provider categories that look adult, for <see cref="ProviderProfile.SkipAdultCategories"/>.
/// </summary>
internal static partial class AdultCategory
{
    /// <summary>
    /// Gets a value indicating whether a profile skips a category with this name.
    /// </summary>
    /// <param name="profile">Provider profile.</param>
    /// <param name="name">Category name, if known.</param>
    /// <returns>True when the category is skipped.</returns>
    public static bool Skips(ProviderProfile profile, string? name)
        => profile.SkipAdultCategories && name is not null && Pattern().IsMatch(name);

    [GeneratedRegex(@"\b(adult|xxx|porn|18\+)", RegexOptions.IgnoreCase, 100)]
    private static partial Regex Pattern();
}
