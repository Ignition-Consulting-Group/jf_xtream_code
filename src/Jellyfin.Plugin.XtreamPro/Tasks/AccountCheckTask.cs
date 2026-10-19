using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.XtreamPro.Api;
using Jellyfin.Plugin.XtreamPro.Configuration;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.XtreamPro.Tasks;

/// <summary>
/// Checks each provider account and records its status and connection limit.
/// </summary>
public class AccountCheckTask : IScheduledTask
{
    private static readonly TimeSpan ExpiryWarning = TimeSpan.FromDays(7);

    private readonly IXtreamSettings _settings;
    private readonly AccountMonitor _accounts;
    private readonly ILogger<AccountCheckTask> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AccountCheckTask"/> class.
    /// </summary>
    /// <param name="settings">Plugin settings.</param>
    /// <param name="accounts">Account info cache.</param>
    /// <param name="logger">Logger.</param>
    public AccountCheckTask(IXtreamSettings settings, AccountMonitor accounts, ILogger<AccountCheckTask> logger)
    {
        _settings = settings;
        _accounts = accounts;
        _logger = logger;
    }

    /// <inheritdoc />
    public string Name => "Check Xtream accounts";

    /// <inheritdoc />
    public string Key => "XtreamProAccountCheck";

    /// <inheritdoc />
    public string Description => "Checks each Xtream account's status, expiry and connection limit.";

    /// <inheritdoc />
    public string Category => "Xtream Pro";

    /// <inheritdoc />
    public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(progress);
        var providers = _settings.Current.Providers.ToList();
        for (var i = 0; i < providers.Count; i++)
        {
            var profile = providers[i];
            try
            {
                var account = await _accounts.GetAsync(profile, TimeSpan.Zero, cancellationToken).ConfigureAwait(false);
                if (!account.UserInfo.Auth)
                {
                    _logger.LogWarning("Provider {Provider} rejected the login", profile.Name);
                }
                else if (account.UserInfo.ExpiresAt - DateTimeOffset.UtcNow < ExpiryWarning)
                {
                    _logger.LogWarning("Provider {Provider} account expires {Expiry}", profile.Name, account.UserInfo.ExpiresAt);
                }
            }
            catch (Exception ex) when (ex is HttpRequestException or ArgumentException or System.Text.Json.JsonException)
            {
                _logger.LogError(ex, "Could not check account for {Provider}", profile.Name);
            }

            progress.Report(100.0 * (i + 1) / providers.Count);
        }
    }

    /// <inheritdoc />
    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
        => [new TaskTriggerInfo { Type = TaskTriggerInfoType.IntervalTrigger, IntervalTicks = TimeSpan.FromHours(6).Ticks }];
}
