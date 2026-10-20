# 0004 - Live TV through a tuner host and listings provider

## Status

Accepted

## Context

Jellyfin 12.2 resolves `ITunerHost` and `IListingsProvider` from DI and iterates every registered tuner host, whether or not the user added a tuner entry in Live TV settings. Guide data, however, only flows from listings providers that have an entry in `livetv.xml`, and channels are matched to guide channels by id. Jellyfin also only enables Live TV for users (the Live TV view and `EnabledUsers`) and its own "Refresh Guide" task when `livetv.xml` lists at least one tuner.

## Decision

- `XtreamTunerHost` serves channels from every enabled provider profile. Channel ids are `xtreampro_{providerId:N}_{streamId}`; ids that do not match are left to other tuners (empty media sources, `FileNotFoundException` on open, which Jellyfin expects).
- `XtreamListingsProvider` returns the same channel ids, so Jellyfin matches guide channels without any mapping.
- `ListingsRegistration` adds a listings entry and a tuner entry (named "Xtream Pro") to `livetv.xml` on startup. The tuner host ignores the tuner entry; it exists so Jellyfin shows Live TV to users. It writes the config directly because `IListingsManager.SaveListingProvider` queues a scheduled task, which is not possible before Jellyfin has registered its tasks. Failures are logged and never block startup.
- Our "Refresh Xtream guide" task downloads XMLTV with a forward-only reader, fills channels with no current data from `get_short_epg` (capped at 200 calls), stores the result per provider on disk, and then queues Jellyfin's own guide refresh.
- Opening a channel takes a slot from `ConnectionTracker` against the account's `max_connections` and throws `LiveTvConflictException` when none is free. Stream sharing is off so every viewer is counted.
- Jellyfin reads the provider URL directly. TS streams may be direct played when the profile prefers it; HLS is never direct played because its playlist URLs are relative to the provider.

## Consequences

No manual Live TV setup is needed. Saving changed providers queues "Refresh Xtream guide", which queues Jellyfin's guide refresh in turn, so channels appear without waiting for the schedule. Live TV settings show the tuner as "Xtream Pro" and the guide provider as "Unknown", because jellyfin-web only names its built-in types. Changing a provider profile's id changes its channel ids, so the config page must keep ids stable.
