## Why

Core library API surfaces currently require an ad hoc GenAPI build and invocation, making regeneration difficult to reproduce and review. The repository needs a small, self-contained tool that can regenerate the surfaces on demand and refresh its GenAPI implementation when needed.

## What Changes

- Add a repository-owned GenAPI wrapper that fetches an ignored source snapshot pinned to the `dotnet/sdk` revision matching `global.json`.
- Add one command that builds every project under `src/Core` and regenerates its public API listing with fully resolved framework and package references.
- Add one command that updates the vendored GenAPI implementation from the `dotnet/sdk` revision matching `global.json`.
- Write generated API listings to the git-ignored `artifacts` directory for local review without adding generated files to source control.
- Keep all wrapper implementation, scripts, MSBuild integration, documentation, and provenance under `tools/ApiSurface`, while ignoring the fetched upstream source.

## Capabilities

### New Capabilities

- `core-api-surface-generation`: On-demand generation of public API listings for every project under `src/Core` and on-demand refresh of the underlying GenAPI implementation.

### Modified Capabilities

None.

## Impact

- Adds a self-contained utility under `tools/ApiSurface/`.
- Writes ignored review output under `artifacts/api-surface/Core/`.
- Introduces no runtime product behavior or public API changes.
- Uses the SDK pinned by `global.json`.
