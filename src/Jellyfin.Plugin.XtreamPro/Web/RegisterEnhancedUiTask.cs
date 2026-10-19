using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.XtreamPro.Configuration;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.XtreamPro.Web;

/// <summary>
/// Links jellyfin-web to the enhanced Live TV page at startup. If that fails the page stays reachable by URL and the link is skipped.
/// </summary>
public class RegisterEnhancedUiTask : IScheduledTask, IConfigurableScheduledTask
{
    private static readonly Guid TransformationId = Guid.Parse("4f6e1c1e-5a0f-4d8e-9a59-7b0f3f6c2e11");

    private readonly IXtreamSettings _settings;
    private readonly EnhancedUiStatus _status;
    private readonly ILogger<RegisterEnhancedUiTask> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="RegisterEnhancedUiTask"/> class.
    /// </summary>
    /// <param name="settings">Plugin settings.</param>
    /// <param name="status">Status shown on the dashboard.</param>
    /// <param name="logger">Logger.</param>
    public RegisterEnhancedUiTask(IXtreamSettings settings, EnhancedUiStatus status, ILogger<RegisterEnhancedUiTask> logger)
    {
        _settings = settings;
        _status = status;
        _logger = logger;
    }

    /// <inheritdoc />
    public string Name => "Link the enhanced Live TV page";

    /// <inheritdoc />
    public string Key => "XtreamProRegisterEnhancedUi";

    /// <inheritdoc />
    public string Description => "Adds a link to the enhanced Live TV page in the web client.";

    /// <inheritdoc />
    public string Category => "Xtream Pro";

    /// <inheritdoc />
    public bool IsHidden => true;

    /// <inheritdoc />
    public bool IsEnabled => true;

    /// <inheritdoc />
    public bool IsLogged => false;

    /// <inheritdoc />
    public Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        if (!_settings.Current.EnableEnhancedLiveTv)
        {
            _status.Message = "The enhanced Live TV page is turned off.";
            return Task.CompletedTask;
        }

        _status.Injected = FileTransformationBridge.TryRegister(TransformationId, "index.html", typeof(IndexTransformer), nameof(IndexTransformer.Transform), out var error);
        _status.Message = _status.Injected ? "Linked from the web client menu." : error + " It is still available at /XtreamPro/Web/.";
        if (!_status.Injected)
        {
            _logger.LogWarning("Enhanced Live TV link not added: {Reason}", error);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
        => [new TaskTriggerInfo { Type = TaskTriggerInfoType.StartupTrigger }];
}
