# Claude Code Kickoff Prompt - Jellyfin Xtream Pro Plugin

> Drop this into the repo root as `CLAUDE.md` (or paste it as the first message in Claude Code). Working name: **Jellyfin.Plugin.XtreamPro**. Rename freely.

---

## 1. Mission

Build a Jellyfin server plugin that connects to Xtream Codes compatible IPTV providers and delivers an experience on par with IPTV Smarters Pro: fast live TV with a full EPG, catch-up, and VOD and series that behave like first-class Jellyfin libraries (posters, resume, search, continue watching, collections).

Existing Xtream plugins expose VOD and series through Jellyfin's Channels feature, which gives a clunky UI with weak metadata. This plugin must not do that. VOD and series are synced into real libraries via `.strm` + `.nfo` files.

The plugin is a client for a service the user already subscribes to. The README must state that users need a legitimate subscription and that no content or provider details are bundled.

## 2. Ground rules for you (Claude Code)

- **TDD is mandatory.** Red, green, refactor. Write a failing test first, commit-sized steps, no production code without a test that demanded it. Exceptions only for pure wiring (DI registration, plugin manifest) and must be called out in the commit message.
- **Plan before each phase.** Use plan mode, present the plan, wait for my approval at every phase gate (marked with a gate symbol below). Do not start the next phase without it.
- **Small commits, Conventional Commits** (`feat:`, `fix:`, `test:`, `refactor:`, `chore:`, `ci:`, `docs:`). One logical change per commit.
- **Keep the docs alive:** update `docs/PROGRESS.md` at the end of every session and record significant decisions as short ADRs in `docs/adr/NNNN-title.md`.
- **Never commit credentials.** Provider URL, username and password come from environment variables or the plugin config only. Test fixtures must be scrubbed (fake hosts, fake usernames, fake stream IDs).
- **Verify Jellyfin APIs, don't guess.** Before using any Jellyfin interface, inspect the `Jellyfin.Controller` / `Jellyfin.Model` packages for the pinned version (decompile or read source on GitHub for that tag). If an interface differs from what this prompt describes, follow the real API and log it in an ADR.
- Writing style in docs and UI copy: plain, direct, regular hyphens only (no em dashes).

## 3. Target platform

- Step zero: confirm the current stable Jellyfin release and its .NET version, then pin `targetAbi`, `Jellyfin.Controller` and `Jellyfin.Model` to it. Record in ADR 0001.
- Language: C# (latest supported by that .NET), nullable enabled, warnings as errors, analyzers on (`Microsoft.CodeAnalysis.NetAnalyzers`, StyleCop or equivalent used by the Jellyfin plugin template).
- Start from `jellyfin/jellyfin-plugin-template`. Use the existing community Xtream plugin (Kevinjil) as a reference for API quirks only; do not copy its architecture.

## 4. Architecture

```
src/
  Jellyfin.Plugin.XtreamPro/
    Api/              Xtream HTTP client, DTOs, URL builder
    Sync/             Catalogue diff engine, .strm/.nfo writer, scheduled tasks
    LiveTv/           Tuner host, listings (EPG) provider, channel mapping
    CatchUp/          Timeshift URL building, archive window logic
    Configuration/    Plugin config model + embedded config page
    Web/              Enhanced UI assets (Phase 6)
    Plugin.cs, PluginServiceRegistrator.cs
tests/
  XtreamPro.UnitTests/
  XtreamPro.IntegrationTests/
  XtreamPro.UiTests/            (Playwright)
tools/
  FakeXtreamServer/             Minimal ASP.NET app emulating a provider
docker/
  docker-compose.dev.yml        Jellyfin + FakeXtreamServer
```

### 4.1 Xtream API client
Endpoints on `player_api.php?username=&password=&action=`:
`(none - auth/account info)`, `get_live_categories`, `get_live_streams`, `get_vod_categories`, `get_vod_streams`, `get_vod_info`, `get_series_categories`, `get_series`, `get_series_info`, `get_short_epg`, `get_simple_data_table`. Full EPG from `xmltv.php`.

Stream URLs:
- Live: `{base}/live/{user}/{pass}/{id}.{ts|m3u8}`
- Movie: `{base}/movie/{user}/{pass}/{id}.{container_extension}`
- Episode: `{base}/series/{user}/{pass}/{episode_id}.{container_extension}`
- Catch-up: `{base}/timeshift/{user}/{pass}/{duration_min}/{YYYY-MM-DD:HH-mm}/{id}.ts`

Requirements:
- Tolerant JSON parsing. Providers return numbers as strings, empty strings for nulls, arrays where objects are expected (and vice versa), and inconsistent casing. Every quirk gets a fixture and a test.
- Configurable User-Agent per provider (default to a common player UA).
- Respect `max_connections` and `active_cons` from account info. Expose current usage in the config page.
- Retries with jittered backoff (Polly or `Microsoft.Extensions.Http.Resilience`), timeouts, and a circuit breaker per provider.
- Never probe streams during browse or sync. Probing burns connections.

### 4.2 Live TV
- Prefer Jellyfin's `ITunerHost` + `IListingsProvider` pattern so channels and the guide flow through native Live TV. Fall back to `ILiveTvService` only if the pinned version requires it (ADR).
- Channel numbering: stable, user-overridable, ordered by category then provider `num`.
- Category include/exclude and rename; map categories to channel tags/groups.
- Logos from `stream_icon`, with fallback.
- EPG: `xmltv.php` on a schedule, streamed parse (files can be 100MB+), `get_short_epg` as a fill-in for channels with gaps. Timezone and offset correction setting.
- Output format setting (TS or HLS) and a "direct play preferred" toggle to minimise channel-switch latency.

### 4.3 VOD and series library sync
- User picks a target folder per type (e.g. `/media/xtream/movies`, `/media/xtream/shows`) and adds it as a normal Jellyfin library.
- Writer produces Jellyfin-friendly naming:
  - `Movies/Title (Year)/Title (Year).strm` + `movie.nfo`
  - `Shows/Series Name (Year)/Season 01/Series Name - S01E01 - Episode Title.strm` + `tvshow.nfo`, `episode.nfo`
- `.nfo` carries TMDB IDs when the provider supplies them (so Jellyfin matches cleanly), plus plot, genres, rating, artwork URLs.
- `.strm` must not embed credentials in plain URLs if avoidable. Preferred: point `.strm` at a plugin endpoint (`/XtreamPro/Stream/{type}/{id}`) that redirects to the real URL at play time. This also survives password changes without a resync. ADR this decision.
- Incremental diff engine: a local state store (SQLite or JSON) of what was written; add new, update changed, remove deleted. Never full-rewrite. Must handle 50k+ VOD items in batches without blocking the server.
- Category filters, a "skip adult categories" default, and language/prefix stripping for titles (providers often prefix "EN | ", "4K - " etc.) with configurable regex rules.
- Series info fetched lazily and cached; refresh window configurable.

### 4.4 Catch-up
- For channels with `tv_archive=1`, expose past programmes within `tv_archive_duration` days.
- Surface as recordings-style items or a "Catch-up" library (decide in plan, ADR).

### 4.5 Scheduled tasks
- Account check, live channel refresh, EPG refresh, VOD sync, series sync. Each independently scheduled with sensible defaults and progress reporting.

## 5. UI - make it better than the rest

Be explicit with me about the boundary: native clients (Android TV, Swiftfin, Roku, etc.) render their own UI and a plugin cannot change it. The web UI (jellyfin-web, and clients that wrap it like Jellyfin Media Player and the mobile apps) can be extended.

### 5.1 Data-shaping wins (benefit every client)
- Real Movies/Shows libraries via `.strm` (section 4.3).
- Auto-generated Collections per provider category (BoxSets), optional.
- Clean titles, artwork, ratings, TMDB matching.
- Sensible channel order, numbers, groups and logos.

### 5.2 Plugin config page (fully ours)
A modern, responsive dashboard page, not a wall of inputs:
- Provider profiles (multiple), with "Test connection" showing account status, expiry, connection usage.
- Category manager: searchable, bulk include/exclude, rename, reorder, per-type tabs (Live / VOD / Series).
- Title cleanup rules with a live preview.
- Sync status: last run, counts added/updated/removed, errors, "run now".
- EPG health: channels with no guide data, mapping overrides.
- Respects Jellyfin dashboard theme (light/dark), keyboard accessible.

### 5.3 Enhanced web UI (Phase 6, behind a feature flag)
A Smarters-style Live TV experience inside jellyfin-web:
- Left sidebar of categories, channel list with logos and now/next, live preview pane, compact EPG grid, favourites, recent channels, number-key channel jump, up/down zapping.
- Delivered as a plugin-served page (`/XtreamPro/Web/...`) linked from the web client, using a tab/home-section injection approach compatible with existing community injection plugins (investigate File Transformation / Custom Tabs style approaches for the pinned version; ADR the choice).
- Uses only Jellyfin's own APIs from the browser (authenticated as the current user), so playback still goes through Jellyfin.
- Must degrade gracefully: if injection fails after a Jellyfin update, the plugin still works and the feature turns itself off with a warning.

## 6. Testing strategy

| Layer | Tools | What |
|---|---|---|
| Unit | xUnit, NSubstitute, Shouldly (or AwesomeAssertions; avoid FluentAssertions v8+ licensing) | Parsers, URL builder, diff engine, naming, title rules, EPG parsing |
| Snapshot | Verify | `.nfo` and `.strm` output, config page JSON contracts |
| Contract | Recorded fixtures | Every provider quirk; add a fixture each time a real provider breaks something |
| Integration | Testcontainers or docker compose, FakeXtreamServer | Plugin loads in real Jellyfin, sync writes a library, Live TV channels and guide appear via Jellyfin REST API |
| UI | Playwright | Config page flows, enhanced Live TV page (zapping, EPG render) |
| Perf | BenchmarkDotNet + a 50k-item fixture | Sync time and memory ceilings, EPG parse of a large XMLTV |

FakeXtreamServer must support: configurable catalogue size, quirk toggles (string numbers, empty arrays, bad dates), a max-connections limit that returns the real error behaviour, and a tiny looping TS/HLS stream for playback tests.

Coverage gate: 80% line coverage on `Api`, `Sync`, `LiveTv`, `CatchUp`. Don't chase coverage on wiring.

## 7. DevOps

### 7.1 Repo and workflow
- GitHub repo, trunk-based: short-lived branches `feat/...`, `fix/...`, squash-merge PRs into `main`.
- Branch protection: CI green, linear history.
- `.editorconfig`, `dotnet format` enforced, pre-commit hook (Husky.Net or lefthook) running format + unit tests.
- Dependabot for NuGet, npm (Playwright) and GitHub Actions.
- `CODEOWNERS`, PR template with a TDD checklist, issue templates (bug with provider-quirk section, feature).

### 7.2 CI (GitHub Actions)
- `ci.yml` on PR and push: restore, build (warnings as errors), unit + snapshot tests, coverage report and gate, `dotnet format --verify-no-changes`.
- `integration.yml`: spin up Jellyfin (pinned image tag) + FakeXtreamServer, install the built plugin, run integration and Playwright tests. Upload Jellyfin logs and Playwright traces as artifacts on failure.
- `codeql.yml` for C# and JS; secret scanning on.

### 7.3 Release
- SemVer, driven by Conventional Commits (release-please).
- `release.yml` on tag: build, package the plugin zip, compute checksum, publish a GitHub Release, and update `manifest.json` (Jellyfin plugin repository format) served from GitHub Pages so users can add the repo URL in Jellyfin and get updates.
- `CHANGELOG.md` generated.
- A compatibility matrix in the README: plugin version vs Jellyfin version.

### 7.4 Local dev loop
- `docker/docker-compose.dev.yml`: Jellyfin with the plugin output folder mounted, FakeXtreamServer, optional real-provider profile via `.env` (git-ignored).
- `make` (or a `justfile`) targets: `build`, `test`, `test-int`, `dev-up`, `dev-reload` (build, copy DLL, restart Jellyfin, tail logs), `release-dry`.

### 7.5 Work tracking
- Mirror the phases below as epics and the bullets as tasks in a tracking board (GitHub Projects by default, or a monday.com board via MCP if connected; use monday API version 2025-04 for any scripted calls).
- Close tasks from commits/PRs where possible.

## 8. Phased delivery (gate = stop and wait for my review)

**Phase 0 - Foundations**
Confirm Jellyfin version (ADR 0001), scaffold from template, solution layout, analyzers, test projects, FakeXtreamServer skeleton, docker dev stack, CI (`ci.yml`), pre-commit hooks, README skeleton, PROGRESS.md. Plugin loads in dev Jellyfin and shows an empty config page. 🚦

**Phase 1 - Xtream client**
TDD the DTOs, tolerant parsers, URL builder, account info, resilience policies, connection tracking. Fixture library of quirks. 🚦

**Phase 2 - Live TV + EPG**
Tuner host, channel mapping, numbering, category filters, XMLTV streaming parser, short-EPG fill, timezone offset. Integration test proves channels + guide appear in Jellyfin and a channel plays from FakeXtreamServer. 🚦

**Phase 3 - VOD and series sync**
Stream redirect endpoint, `.strm`/`.nfo` writer with snapshot tests, diff engine + state store, batching, title cleanup rules, scheduled tasks. Integration test: a fresh sync creates a library Jellyfin scans correctly; a second sync with changes only touches the diff. Perf benchmark at 50k items. 🚦

**Phase 4 - Catch-up**
Archive detection, timeshift URLs, presentation approach per ADR, integration test. 🚦

**Phase 5 - Config dashboard**
Full config page from section 5.2, Playwright tests, light/dark, accessibility pass. 🚦

**Phase 6 - Enhanced web UI**
Injection approach spike first (ADR), then the Smarters-style Live TV page behind a feature flag, Playwright tests, graceful degradation test. 🚦

**Phase 7 - Release**
`integration.yml`, `codeql.yml`, `release.yml`, release-please, plugin repo manifest on GitHub Pages, README with install steps, screenshots and compatibility matrix, v1.0.0. 🚦

## 9. Definition of done (every task)
- Tests written first and passing; coverage gate holds.
- No new analyzer warnings; `dotnet format` clean.
- Docs updated (PROGRESS.md, ADR if a decision was made, README if user-facing).
- Works against FakeXtreamServer in docker; if a real provider is configured locally, smoke-tested against it.

## 10. First action
Start Phase 0. Before writing code: confirm the current stable Jellyfin version and .NET target, summarise the relevant Live TV and library interfaces for that version, list any deviations from this prompt, and present the Phase 0 plan for approval.
