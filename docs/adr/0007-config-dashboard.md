# 0007 - Config dashboard

## Status

Accepted

## Context

The plugin page is loaded by jellyfin-web inside the dashboard. It must follow the dashboard theme, work from the keyboard, and stay usable after Jellyfin updates.

## Decision

- One embedded HTML page with an inline script and no framework or build step. Controls are Jellyfin's own `emby-*` elements and layout classes, so they inherit the light or dark theme; the few custom styles use `currentColor` and translucent borders, never fixed colours.
- Tabs follow the ARIA tab pattern (arrow keys move between tabs), every input has a label, and results are announced through `role="status"` regions.
- The page talks to admin-only endpoints under `/XtreamPro` (`RequiresElevation`): test connection, list categories, title preview, task status and guide health. Test connection and category listing take the provider from the form, so they work before saving. "Run now" uses Jellyfin's scheduled task API.
- jellyfin-web replaces `${...}` in plugin pages with translations, so the script uses string concatenation, never template literals. A unit test fails if `${` appears in the page.
- UI tests use Playwright for .NET against the same Jellyfin container fixture as the integration tests. The browser is signed in by seeding `jellyfin_credentials` in local storage.

## Consequences

Provider ids are created in the browser (`crypto.randomUUID`) and kept on save, so channel ids stay stable (ADR 0004).
