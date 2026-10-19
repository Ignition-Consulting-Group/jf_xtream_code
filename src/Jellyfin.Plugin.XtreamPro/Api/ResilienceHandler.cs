using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Jellyfin.Plugin.XtreamPro.Api;

/// <summary>
/// Runs provider requests through the pipeline for their host.
/// </summary>
public class ResilienceHandler : DelegatingHandler
{
    private readonly XtreamResilience _resilience;

    /// <summary>
    /// Initializes a new instance of the <see cref="ResilienceHandler"/> class.
    /// </summary>
    /// <param name="resilience">Pipelines keyed by host.</param>
    public ResilienceHandler(XtreamResilience resilience)
    {
        _resilience = resilience;
    }

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var pipeline = _resilience.For(request.RequestUri!.Authority);
        return await pipeline.ExecuteAsync(
            static async (state, ct) => await state.Handler.BaseSendAsync(state.Request, ct).ConfigureAwait(false),
            (Handler: this, Request: request),
            cancellationToken).ConfigureAwait(false);
    }

    private Task<HttpResponseMessage> BaseSendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        => base.SendAsync(request, cancellationToken);
}
