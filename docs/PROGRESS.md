# Progress

## Phase 3 - VOD and series sync (done)

- Signed stream redirect endpoint so `.strm` files hold no credentials ([ADR 0005](adr/0005-vod-and-series-as-strm-libraries.md)).
- Title cleanup rules (provider prefixes, quality tags, year extraction), Jellyfin-friendly naming with `[tmdbid-N]` tags, approved-file snapshot tests for every `.nfo`.
- Incremental writer with per-file hashes: adds, updates and removes only what changed; empty catalogues and failed series lookups never delete files.
- Category filters, adult categories skipped by default, optional collection per category.
- Series episode lists cached with a configurable refresh window.
- Scheduled tasks: sync movies (24h), sync series (24h); a library scan is queued when files changed.
- FakeXtreamServer: movies, series, catalogue size settings, quirk toggles (string numbers, empty arrays, bad dates), movie and episode streams.
- Integration test: a fresh sync creates movies and shows Jellyfin recognises, playback goes through the signed redirect, and a second sync writes no files.
- Benchmark (50k movies): unchanged resync 0.8s; first sync is bound by disk writes.

## Phase 2 - Live TV and EPG (done)

- `XtreamTunerHost` and `XtreamListingsProvider` registered through DI; the guide provider entry is added automatically ([ADR 0004](adr/0004-live-tv-through-tuner-host-and-listings-provider.md)).
- Channel mapping: order by category then provider number, user number overrides, category exclude and rename, logos with XMLTV icon fallback, category as channel group and tag.
- Streaming XMLTV reader (gzip sniffing, missing timezones, short time formats, missing stop times), timezone offset setting, `get_short_epg` fill for channels without data.
- Output format (TS or HLS) and direct play preference per provider.
- Connection limits enforced when opening a channel.
- Scheduled tasks: refresh guide (12h), refresh channels (6h), account check (6h).
- FakeXtreamServer: live categories, channels, XMLTV, short EPG, a looping TS stream and a max connections limit.
- Integration test: channels and guide appear in Jellyfin 12.2 and a channel opens a stream from FakeXtreamServer.

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
