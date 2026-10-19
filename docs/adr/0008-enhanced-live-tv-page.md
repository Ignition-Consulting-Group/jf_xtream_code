# 0008 - Enhanced Live TV page and how it is linked

## Status

Accepted

## Context

Native clients draw their own UI, so a Smarters-style Live TV experience can only live in jellyfin-web and the clients that wrap it. Jellyfin 12.2 has no supported way for a plugin to add a page or menu entry for ordinary users. The community File Transformation plugin (v12 compatible) lets plugins rewrite web client files in memory, and is what Plugin Pages, Home Sections and Custom Tabs use.

Spike findings:

- File Transformation registers by reflection on `Jellyfin.Plugin.FileTransformation.PluginInterface.RegisterTransformation(JObject)`. The plugin builds the `JObject` with the Newtonsoft assembly from File Transformation's own load context, so there is no Newtonsoft dependency here.
- A page served by the plugin at `/XtreamPro/Web/` shares the web client's origin, so it can read the signed-in session from `jellyfin_credentials` in local storage and call Jellyfin's API as the current user. Playback goes through Jellyfin (`PlaybackInfo` then the HLS `TranscodingUrl`), played with hls.js.
- The web client drawer is `.mainDrawer-scrollContainer` in 12.2.

## Decision

- The page is behind the `EnableEnhancedLiveTv` flag, off by default.
- A hidden startup task registers an `index.html` transformation that adds one `<script src="/XtreamPro/Web/inject.js">`. The loader adds a "Live TV+" entry to the drawer and puts it back whenever the web client rebuilds the drawer.
- Degradation: if File Transformation is missing or rejects the registration, the task logs a warning, the dashboard shows why, and the page is still reachable by URL. If the drawer is not found the loader logs one console warning and does nothing. If the flag is turned off, the loader is served empty and the page returns 404, so a stale injection is harmless.
- Tuning is debounced and serialised, and the open stream is closed on channel change and when the page is left, so zapping never holds more than one provider connection.

## Consequences

The link appears only for users of File Transformation. If a tab is closed while a channel is still opening (about three seconds), Jellyfin keeps that live stream open until its session cleanup, holding one provider connection meanwhile.
