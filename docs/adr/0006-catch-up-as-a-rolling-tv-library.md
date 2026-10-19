# 0006 - Catch-up as a rolling TV library

## Status

Accepted

## Context

Channels with `tv_archive=1` let viewers play past programmes for `tv_archive_duration` days through timeshift URLs. Jellyfin's recordings model belongs to `ILiveTvService`, which this plugin does not use (ADR 0004), and native clients would not show provider archives there anyway.

## Decision

- Past programmes are written to a "Catch-up" folder that the user adds as a Shows library: one show per archive channel, `Season {year}`, and one episode per finished programme in the window, named `Channel - yyyy-MM-dd HH.mm - Title` with an NFO carrying the title, plot, air date, season (year) and episode (`MMddHHmm`) so episodes sort by time.
- Programmes come from the stored guide, so no extra provider calls are made. The "Sync Xtream catch-up" task runs hourly through the same incremental writer, which removes programmes once they leave the window. Here an empty plan is valid and does remove everything.
- Each `.strm` points at `/XtreamPro/Stream/{provider}/catchup/{stream}.ts?start=&minutes=&sig=`. Start and length are signed. The endpoint converts the start to the provider's `server_info.timezone` (UTC if unknown) and redirects to the timeshift URL.
- Programmes with the same start on one channel (overlapping guide entries) become one episode.

## Consequences

Catch-up works in every Jellyfin client. File names and air dates use the Jellyfin server's time zone. Like VOD, Jellyfin probes a catch-up stream only when it is played; the integration tests check that syncing and scanning never open a stream.
