## Purpose

Keep telemetry trace buffering and confirmed tenant-boundary caching under the same configurable Azclips home as profiles and authentication state.

## ADDED Requirements

### Requirement: Unified default storage root

Telemetry SHALL buffer traces under `~/.azclips/traces` and store confirmed tenant boundaries in `~/.azclips/data-boundary-cache.json`. Startup cached routing SHALL read the active profile from `~/.azclips`.

#### Scenario: Default home

- **WHEN** `AZCLIPS_HOME` is unset
- **THEN** trace producers, uploader coordination, boundary caching, and startup profile reads use the user-profile Azclips home

### Requirement: Home override

Telemetry SHALL honor `AZCLIPS_HOME` for traces, boundary caching, and startup profile reads.

#### Scenario: Isolated home

- **WHEN** `AZCLIPS_HOME` names an alternate directory
- **THEN** traces use its `traces` child and the cache and profile files use that alternate directory

### Requirement: No legacy migration

Telemetry SHALL NOT automatically read or migrate traces or boundary-cache files from the previous local application data root when it differs from the selected home.

#### Scenario: Old files exist

- **WHEN** trace or boundary-cache files exist only in the previous local application data root
- **THEN** they remain untouched and are not used by the new storage paths
