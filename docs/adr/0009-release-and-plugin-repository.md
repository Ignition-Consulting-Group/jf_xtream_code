# 0009 - Release and plugin repository

## Status

Accepted

## Context

Users install Jellyfin plugins from repository manifests. Versions must follow SemVer from Conventional Commits, and each release needs a zip and a checksum.

## Decision

- release-please (simple release type) keeps `CHANGELOG.md`, the version in `src/Jellyfin.Plugin.XtreamPro/build.yaml` and the release PR. The first release was pinned to 1.0.0 with `release-as`, removed once it shipped.
- The publish job runs in the same workflow when release-please creates a release, because tags pushed with the workflow token do not start other workflows.
- The zip and `meta.json` come from Jellyfin's own `jprm` tool using `build.yaml`, so the manifest matches what Jellyfin expects (MD5 checksum, targetAbi). A SHA-256 `checksums.sha256` is attached for manual downloads.
- `manifest.json` lives on the `gh-pages` branch and is served by GitHub Pages.
- Tags are plain `vX.Y.Z`. PRs are squash-merged with a Conventional Commit title, because release-please only reads commits on `main`.

## Consequences

GitHub Pages must be enabled for the `gh-pages` branch, and Actions must be allowed to create pull requests, in the repository settings. If the repository is private, the manifest URL is only reachable where Pages is public.
