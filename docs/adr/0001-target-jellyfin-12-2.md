# 0001 - Target Jellyfin 12.2 on .NET 10

## Status

Accepted

## Context

The current stable Jellyfin release is 12.2.0 (October 2026). `Jellyfin.Controller` and `Jellyfin.Model` 12.2.0 target `net10.0`. The official plugin template still targets 10.11 and .NET 9.

## Decision

- `targetAbi` 12.2.0.0, `Jellyfin.Controller` and `Jellyfin.Model` 12.2.0, `net10.0`.
- Dev and CI use the `jellyfin/jellyfin:12.2.20261005-225228` image (there is no `12.2.0` tag).
- Bundled `Microsoft.Extensions.*` dependencies stay on the 10.0.x line the server ships, to avoid version clashes in the plugin load context.

## Live TV and library interfaces in 12.2

- `ITunerHost` and `IListingsProvider` are resolved from DI as `IEnumerable<>` by `TunerHostManager` and `ListingsManager`, so a plugin can register its own through `IPluginServiceRegistrator`. `ILiveTvService` is not needed.
- `ITunerHost`: `GetChannels`, `GetChannelStream`, `GetChannelStreamMediaSources`, `DiscoverDevices`. `IConfigurableTunerHost.Validate(TunerHostInfo)` for config validation.
- `IListingsProvider`: `GetProgramsAsync`, `Validate`, `GetLineups`, `GetChannels`.
- The dashboard config page endpoint `/web/ConfigurationPage` is anonymous; plugin pages come from `IHasWebPages`.

## Consequences

Users must be on Jellyfin 12.2 or later. The plugin folder must be writable because Jellyfin writes `meta.json` next to the DLL on first load.
