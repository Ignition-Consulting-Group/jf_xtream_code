using System;

namespace Jellyfin.Plugin.XtreamPro.Api;

/// <summary>
/// Retry, timeout and circuit breaker settings for provider calls.
/// </summary>
public sealed record XtreamResilienceOptions
{
    /// <summary>Gets the number of retries after the first attempt.</summary>
    public int RetryCount { get; init; } = 3;

    /// <summary>Gets the base delay for jittered exponential backoff.</summary>
    public TimeSpan BaseDelay { get; init; } = TimeSpan.FromSeconds(1);

    /// <summary>Gets the timeout for a single attempt.</summary>
    public TimeSpan AttemptTimeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>Gets how long the circuit stays open.</summary>
    public TimeSpan BreakDuration { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>Gets the number of calls needed before the circuit can open.</summary>
    public int MinimumThroughput { get; init; } = 5;
}
