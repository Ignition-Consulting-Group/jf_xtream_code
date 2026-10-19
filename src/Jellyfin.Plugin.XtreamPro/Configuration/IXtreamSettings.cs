namespace Jellyfin.Plugin.XtreamPro.Configuration;

/// <summary>
/// Gives services the current plugin configuration.
/// </summary>
public interface IXtreamSettings
{
    /// <summary>
    /// Gets the current configuration.
    /// </summary>
    PluginConfiguration Current { get; }

    /// <summary>
    /// Persists changes made to <see cref="Current"/>.
    /// </summary>
    void Save();
}
