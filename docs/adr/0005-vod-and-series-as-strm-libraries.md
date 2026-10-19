# 0005 - VOD and series as .strm libraries

## Status

Accepted

## Context

VOD and series must behave like normal Jellyfin libraries. Writing `.strm` files with provider URLs would put credentials on disk and break on a password change.

## Decision

- Each `.strm` points at `/XtreamPro/Stream/{providerId}/{movie|episode}/{id}.{ext}?sig=...` on the Jellyfin server. The endpoint is anonymous, because ffmpeg and direct-playing clients do not send a Jellyfin token, so each URL carries an HMAC signature from a per-install secret. The endpoint answers with a 302 to the provider URL built from the current credentials.
- The base address written into `.strm` files is the `StreamBaseUrl` setting, or the server's local address when unset. Clients must be able to reach it.
- Files go straight into the configured movies and shows folders. Shows must be direct children of a TV library root, so there is no per-provider subfolder; when more than one provider syncs the same type, top-level folders get a ` [Provider]` tag.
- Naming: `Title (Year) [tmdbid-N]/Title (Year).strm` + `movie.nfo`; `Show (Year) [tmdbid-N]/Season 01/Show - S01E01 - Title.strm` + a `.nfo` with the same base name (Jellyfin reads episode NFOs by file name, not `episode.nfo`) and `tvshow.nfo`.
- Movies can join a collection per category through `<set>` in `movie.nfo`; Jellyfin builds it when the library has "Automatically add to collection" on.
- A JSON state file per provider and type records a hash per written file. A sync writes only new or changed files, deletes files the provider dropped, and removes empty folders. An empty catalogue never deletes anything, and series whose episode list could not be fetched keep their files.
- `get_series_info` results are cached and refetched only when `last_modified` changes or the refresh window passes. Movies use `get_vod_streams` only, never one call per movie.

## Consequences

The stream redirect does not count against `max_connections` because Jellyfin does not tell the plugin when on-demand playback ends. Providers that require a specific User-Agent for VOD may reject ffmpeg's default one.

50k movies (benchmark, sandbox disk): planning 0.7s, first sync 46s (writing 100k files), unchanged resync 0.8s and 172MB allocated.
