using System;
using System.Net;
using System.Net.Http;
using Polly;
using Polly.CircuitBreaker;
using Polly.Registry;
using Polly.Retry;
using Polly.Timeout;

namespace Jellyfin.Plugin.XtreamPro.Api;

/// <summary>
/// Resilience pipelines keyed by provider host, so one failing provider does not trip another.
/// </summary>
public sealed class XtreamResilience : IDisposable
{
    private readonly ResiliencePipelineRegistry<string> _registry = new();
    private readonly XtreamResilienceOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="XtreamResilience"/> class.
    /// </summary>
    /// <param name="options">Pipeline settings.</param>
    public XtreamResilience(XtreamResilienceOptions options)
    {
        _options = options;
    }

    /// <summary>
    /// Gets the pipeline for a provider host.
    /// </summary>
    /// <param name="authority">Provider host and port.</param>
    /// <returns>The pipeline.</returns>
    public ResiliencePipeline<HttpResponseMessage> For(string authority)
        => _registry.GetOrAddPipeline<HttpResponseMessage>(authority, Build);

    /// <inheritdoc />
    public void Dispose() => _registry.Dispose();

    private void Build(ResiliencePipelineBuilder<HttpResponseMessage> builder)
    {
        var handle = new PredicateBuilder<HttpResponseMessage>()
            .HandleResult(r => r.StatusCode is >= HttpStatusCode.InternalServerError or HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests)
            .Handle<HttpRequestException>()
            .Handle<TimeoutRejectedException>();

        builder
            .AddRetry(new RetryStrategyOptions<HttpResponseMessage>
            {
                ShouldHandle = handle,
                MaxRetryAttempts = _options.RetryCount,
                Delay = _options.BaseDelay,
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                OnRetry = args =>
                {
                    args.Outcome.Result?.Dispose();
                    return default;
                },
            })
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions<HttpResponseMessage>
            {
                ShouldHandle = handle,
                MinimumThroughput = _options.MinimumThroughput,
                FailureRatio = 0.5,
                BreakDuration = _options.BreakDuration,
            })
            .AddTimeout(_options.AttemptTimeout);
    }
}
