# Design

## Context

See [proposal.md](proposal.md) for motivation. Telemetry uses static helpers before dependency injection and in the detached uploader. Existing profile persistence already resolves the user home through `HomePaths`.

## Goals / Non-Goals

**Goals:** Keep producers, reservation files, uploader, cache readers/writers, and startup profile reads on one home root.

**Non-Goals:** Refactor telemetry I/O behind dependency injection, change file formats or routing policy, or migrate existing files.

## Decisions

- Reuse `HomePaths.GetDefaultHomeRoot` rather than independently resolving a user profile. This preserves `AZCLIPS_HOME` everywhere and aligns with #1238.
- Centralize the boundary-cache file path alongside the existing trace directory helper. Retain the local application data helper for compatibility, but stop using it for telemetry.
- Keep current cache locking and trace claim/reservation protocols. Replacing these with generic persistence APIs would expand a path-only change and risk uploader coordination.
- Read the active profile and profiles file from the same home as profile persistence.

## Risks / Trade-offs

- Old buffered traces will no longer upload automatically -> document that old files remain untouched and are not read or migrated.
- A cold boundary cache can require another lookup -> preserve the current timeout and fallback policy.
- Environment overrides can mask default-path regressions -> test both the unset environment and an explicit isolated home.

## Migration Plan

Deploy the new defaults without migration. Cache entries are rebuilt normally; old trace files remain in place. Users needing old buffered traces can move them manually while no Azclips processes are running. Rolling back restores the previous paths without transforming either file format.
