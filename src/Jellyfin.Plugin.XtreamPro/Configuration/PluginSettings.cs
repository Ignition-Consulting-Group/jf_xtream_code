namespace Jellyfin.Plugin.XtreamPro.Configuration;

/// <summary>
/// Reads configuration from the loaded plugin instance.
/// </summary>
internal sealed class PluginSettings : IXtreamSettings
{
    public PluginConfiguration Current => Plugin.Instance?.Configuration ?? new PluginConfiguration();

    public void Save() => Plugin.Instance?.SaveConfiguration();
}
