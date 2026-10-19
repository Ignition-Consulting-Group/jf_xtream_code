# Xtream Pro for Jellyfin

A Jellyfin plugin for Xtream Codes compatible IPTV providers. Live TV and the guide go through Jellyfin's native Live TV, and VOD and series are synced into regular Movies and Shows libraries using `.strm` and `.nfo` files, so they get posters, resume, search and collections like any other media.

**You need a legitimate subscription with an Xtream Codes compatible provider.** This plugin is only a client. It ships no content, playlists or provider details.

## Status

Early development. See [docs/PROGRESS.md](docs/PROGRESS.md).

## Setup

1. Install the plugin and open its settings page.
2. Add a provider with its URL, username and password.
3. For movies and series, set a movies folder and a shows folder, run the "Sync Xtream movies" and "Sync Xtream series" tasks (Dashboard, Scheduled Tasks), then add each folder as a Movies or Shows library.
4. Live TV channels and the guide appear on their own after the "Refresh Xtream guide" task runs.
5. For catch-up, set a catch-up folder and add it as a Shows library. Past programmes on archive channels appear as episodes, one show per channel.

`.strm` files point back at this Jellyfin server, so set "Stream base URL" to an address your clients can reach if the server's local address is not right.

## Compatibility

| Plugin | Jellyfin |
|---|---|
| 0.x | 12.2 |

## Development

Requirements: .NET 10 SDK, Docker, GNU make.

```sh
dotnet tool restore && dotnet husky install   # pre-commit hook: format + unit tests
make test        # unit tests
make coverage    # unit tests with coverage
make test-int    # integration tests, including a real Jellyfin container
make test-ui     # Playwright tests of the settings page (run playwright.ps1 install chromium once)
make dev-up      # Jellyfin on :8096 with the plugin, FakeXtreamServer on :8080
make dev-reload  # rebuild the plugin, restart Jellyfin, tail logs
```

To smoke test against a real provider, copy `.env.example` to `.env` and fill it in. `.env` is git-ignored.

## License

GPL-3.0, as required for plugins that link against Jellyfin.
