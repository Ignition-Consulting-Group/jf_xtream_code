# 0003 - HTTP resilience and host dependencies

## Status

Accepted

## Context

Jellyfin loads each plugin in its own `AssemblyLoadContext`. Any assembly listed in the plugin's `deps.json` and present in its folder is loaded from there, even if the server already has it. Bundling `Microsoft.Extensions.*` would give the plugin its own copies of types like `IServiceCollection` and break registration.

`Microsoft.Extensions.Http.Resilience` pulls in several `Microsoft.Extensions.*` packages the server does not ship, plus some it does, so it would need careful per-assembly exclusion.

## Decision

- Use Polly.Core directly. The server ships Polly.Core 8.7.0, so the plugin compiles against 8.7.0 with `ExcludeAssets=runtime` and uses the server's copy.
- `Microsoft.Extensions.Http` is referenced the same way for `AddHttpClient`.
- A `ResilienceHandler` runs each request through a pipeline from a `ResiliencePipelineRegistry` keyed by provider host: jittered exponential retry on 5xx, 408, 429, network errors and timeouts; a circuit breaker; a per-attempt timeout. One provider failing does not open the circuit for another.
- The plugin output must stay free of bundled framework assemblies. The container test catches regressions.

## Consequences

When Jellyfin bumps Polly.Core, the reference should follow (Dependabot groups it with Jellyfin packages). Requests are GETs without content, so re-sending the same `HttpRequestMessage` on retry is safe.
