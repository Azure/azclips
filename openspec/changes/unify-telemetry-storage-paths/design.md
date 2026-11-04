# Design

## Context

See [proposal.md](proposal.md) for motivation. Telemetry uses static helpers before dependency injection and in the detached uploader. Existing profile persistence already resolves the user home through `HomePaths`.

## Goals / Non-Goals

**Goals:** Keep producers, reservation files, uploader, cache readers/writers, and startup profile reads on one home root.

**Non-Goals:** Change file formats or routing policy, replace trace append/claim protocols with whole-file writes, or migrate existing files.

## Decisions

- Inject `IPersistenceStore`, as in #1238, rather than independently resolving storage roots. The default filesystem store preserves `HomePaths` and `AZCLIPS_HOME`; custom stores remain usable without environment overrides.
- Centralize the boundary-cache file path alongside the existing trace directory helper. Retain the local application data helper for compatibility, but stop using it for telemetry.
- Use byte-oriented persistence reads and atomic writes for the boundary cache, retaining a cross-process lock resolved through the store.
- Keep trace append and claim/reservation protocols on native files resolved through the injected directory provider. Atomic whole-file replacement cannot implement append semantics.
- Read the active profile through `IProfileService`, not a second telemetry-owned JSON parser. Resolve standalone Agent routing asynchronously before its telemetry scope is consumed.
- Pass the injected root to the detached uploader through its child environment, and use the same directory provider for foreground reservations and trace files.

## Risks / Trade-offs

- Old buffered traces will no longer upload automatically -> document that old files remain untouched and are not read or migrated.
- A cold boundary cache can require another lookup -> preserve the current timeout and fallback policy.
- Environment overrides can mask default-path regressions -> test both the unset environment and an explicit isolated home.

## Migration Plan

Deploy the new defaults without migration. Cache entries are rebuilt normally; old trace files remain in place. Users needing old buffered traces can move them manually while no Azclips processes are running. Rolling back restores the previous paths without transforming either file format.
