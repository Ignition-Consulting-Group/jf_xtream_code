# Progress

## Phase 0 - Foundations (done)

- Pinned Jellyfin 12.2.0 on .NET 10 ([ADR 0001](adr/0001-target-jellyfin-12-2.md)).
- Solution layout, analyzers (StyleCop, NetAnalyzers, Serilog, multithreading), warnings as errors.
- Unit and integration test projects on xUnit v3 / Microsoft.Testing.Platform ([ADR 0002](adr/0002-test-stack.md)).
- FakeXtreamServer skeleton answering account info.
- Container test proving the plugin loads in Jellyfin 12.2 and serves its config page.
- Docker dev stack, Makefile, Husky.Net pre-commit hook, CI and integration workflows, Dependabot, templates.

### Not done from here

- Branch protection and the GitHub Projects board need to be set up in the GitHub UI.
- The UI test project (Playwright) is added in Phase 5 when there is UI to test.
