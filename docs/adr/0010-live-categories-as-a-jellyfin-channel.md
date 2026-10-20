# 0010 - Live categories as a Jellyfin channel

## Status

Accepted

## Context

Jellyfin's Live TV guide is a flat channel list in every client. Users want to pick a category first and then a channel, as in IPTV Smarters. The enhanced page ([ADR 0008](0008-enhanced-live-tv-page.md)) does this only in the web client, and native apps such as Roku cannot be changed by a plugin.

## Decision

- The plugin adds a Jellyfin channel (`IChannel`), "Live TV Categories". Its root lists one folder per provider category, in the same order and with the same renames, excludes and adult filter as Live TV. Each folder lists its channels as live video items.
- Every client already browses channels as folders, so this works on Roku, Android TV and the web without client changes.
- Items resolve their stream at play time (`IRequiresMediaInfoCallback`), so a password change needs no refresh. Jellyfin proxies the stream (no direct play), so credentials are not sent to clients.
- Jellyfin saves every item in a channel folder the first time it is opened (about 55 seconds for 5,666 channels in testing). A "Prepare Xtream categories" task, queued after each channel or guide refresh, opens every folder in the background so users never wait (0.1 seconds afterwards).
- A "Show channels in Jellyfin Live TV" setting removes the plugin's tuner and guide entries when off, so only Live TV Categories is shown and Jellyfin skips its guide refresh. It applies on save.
- Jellyfin's web client has no list layout for channel folders and sorts them by name, so a compact view or provider category order cannot come from the plugin there.
- VOD and series stay as `.strm` libraries. Only live channels use this.

## Consequences

Channel items are separate from Live TV channels, so they have no guide data and do not count towards the provider connection limit the tuner enforces. Jellyfin caches each folder for three hours, so new provider channels can take that long to appear here.
