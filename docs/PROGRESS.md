# Progress

## Phase 1 - Xtream client (done)

- URL builder for `player_api.php`, `xmltv.php`, live, movie, episode and timeshift URLs.
- Tolerant JSON: numbers as strings, empty strings as null, `[]` instead of objects, `{}` instead of lists, string-or-array backdrops, episodes keyed by season or as nested arrays, base64 short EPG, mixed casing. Each quirk has a scrubbed fixture.
- `XtreamClient` for every endpoint, with a per-provider User-Agent (default VLC).
- Retry with jitter, per-host circuit breaker and timeouts on Polly.Core ([ADR 0003](adr/0003-http-resilience-and-host-dependencies.md)).
- `ConnectionTracker` to keep playback within `max_connections`. Showing usage on the config page is part of Phase 5.
- Coverage gate at 80% on the gated namespaces is now on (Api at 85%).
- GitHub Actions jobs for PR 1 stayed queued with no runner; checks were run locally before merging.

## Phase 0 - Foundations (done)

- Pinned Jellyfin 12.2.0 on .NET 10 ([ADR 0001](adr/0001-target-jellyfin-12-2.md)).
- Solution layout, analyzers (StyleCop, NetAnalyzers, Serilog, multithreading), warnings as errors.
- Unit and integration test projects on xUnit v3 / Microsoft.Testing.Platform ([ADR 0002](adr/0002-test-stack.md)).
- FakeXtreamServer skeleton answering account info.
- Container test proving the plugin loads in Jellyfin 12.2 and serves its config page.
- Docker dev stack, Makefile, Husky.Net pre-commit hook, CI and integration workflows, Dependabot, templates.

### Not done from here

- Branch protection and the GitHub Projects board need to be set up in the GitHub UI.
- The UI test project (Playwright) is added in Phase 5 when there is UI to test.
