# Proposal

## Why

Profiles and authentication caches moved to the Azclips home in #1238, but telemetry still uses local application data. Unifying these locations also fixes startup routing reading the active profile from the old root.

## What Changes

- **BREAKING**: Store telemetry traces under `~/.azclips/traces` and confirmed tenant boundaries in `~/.azclips/data-boundary-cache.json`.
- Continue honoring `AZCLIPS_HOME` for both paths and resolve startup profiles from that same home.
- Do not read, copy, or migrate files from the previous local application data root.
- Preserve trace formats, routing policy, cache semantics, and uploader coordination.

## Capabilities

### New Capabilities

- `telemetry-storage`: Home-root resolution for telemetry traces, boundary cache, and cached startup routing.

### Modified Capabilities

None.

## Impact

Changes affect shared path helpers and telemetry cache/profile readers, plus path regression tests and storage documentation. No new product dependencies or authentication behavior are required.
