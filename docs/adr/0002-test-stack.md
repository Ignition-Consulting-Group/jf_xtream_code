# 0002 - Test stack

## Status

Accepted

## Context

xUnit v3 4.x only runs on Microsoft.Testing.Platform, which the .NET 10 SDK requires opting into. Verify now requires a paid licence or a declared exemption for commercial users.

## Decision

- xUnit v3 on Microsoft.Testing.Platform (`global.json` test runner).
- Shouldly for assertions and `ShouldMatchApproved` for snapshot tests instead of Verify.
- NSubstitute for doubles, Testcontainers for the real Jellyfin container.
- coverlet.MTP for coverage and the 80% gate on `Api`, `Sync`, `LiveTv` and `CatchUp`. The gate switches on once those namespaces have code.
- Tests that need Docker carry `[Trait("Category", "Container")]` and run in `integration.yml`.

## Consequences

`dotnet test` uses `--project`/`--solution` syntax and MTP filters such as `--filter-not-trait`.
