using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.XtreamPro.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Hosting;

namespace Jellyfin.Plugin.XtreamPro.Tasks;

/// <summary>
/// Refreshes the guide when providers are saved, so channels appear without waiting for the schedule.
/// </summary>
/// <remarks>
/// Jellyfin hides and disables its own guide task when no tuner is listed in Live TV settings, which is the case here.
/// </remarks>
public sealed class RefreshOnSave : IHostedService
{
    private readonly ITaskManager _tasks;
    private readonly BasePlugin<PluginConfiguration>? _plugin;
    private string _providers = string.Empty;

    /// <summary>
    /// Initializes a new instance of the <see cref="RefreshOnSave"/> class.
    /// </summary>
    /// <param name="tasks">Task manager.</param>
    public RefreshOnSave(ITaskManager tasks)
        : this(tasks, Plugin.Instance)
    {
    }

    internal RefreshOnSave(ITaskManager tasks, BasePlugin<PluginConfiguration>? plugin)
    {
        _tasks = tasks;
        _plugin = plugin;
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (_plugin is not null)
        {
            _providers = Serialize(_plugin.Configuration);
            _plugin.ConfigurationChanged += OnConfigurationChanged;
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        if (_plugin is not null)
        {
            _plugin.ConfigurationChanged -= OnConfigurationChanged;
        }

        return Task.CompletedTask;
    }

    private static string Serialize(PluginConfiguration config) => JsonSerializer.Serialize(new { config.Providers, config.ShowLiveTv });

    private void OnConfigurationChanged(object? sender, BasePluginConfiguration e)
    {
        var providers = Serialize((PluginConfiguration)e);
        if (providers != _providers)
        {
            _providers = providers;
            _tasks.QueueScheduledTask<RefreshGuideTask>();
        }
    }
}
