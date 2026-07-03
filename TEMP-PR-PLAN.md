# TEMP PR Plan - Cirrus Reference Sweep

## Scope

Track and remove remaining `Cirrus` references across selected directories.

## Directory Checklist

- [x] `src/PS` (owner: Daniel, references: 0, files: 0)
- [x] `tools/Help` (owner: Daniel, references: 0, files: 0)
- [ ] `tests/Cli.Tests` (owner: Daniel, references: 60, files: 17)
- [x] `src/Extensions` (owner: Daniel, references: 0, files: 0)
- [x] `src/Core` (owner: Daniel, references: 0, files: 0)
- [ ] `tests/Core.Extensions.Tests` (owner: Daniel, references: 36, files: 5)
- [x] `tests/Fixtures` (owner: Daniel, references: 0, files: 0)
- [x] `tests/Cmdlets.Tests` (owner: Daniel, references: 0, files: 0)
- [x] `src/CLI` (owner: Daniel, references: 0, files: 0)
- [ ] `tests/Scripts` (owner: Daniel, references: 109, files: 7)

## Pass Tracking

- [ ] Pass 1: enumerate and categorize references (code/namespace/docs/tests/comments)
- [ ] Pass 2: apply safe text and metadata updates
- [ ] Pass 3: apply code-level renames and fix call sites
- [ ] Pass 4: run validation (build/tests/help checks)
- [ ] Pass 5: final grep and PR notes

## Notes

- Keep compatibility-sensitive renames explicit in PR notes.
- Exclude generated outputs: `obj`, `bin`, `artifacts`, `devops/out`, `.git`.
- src/PS rescan result (2026-07-03): 0 remaining `Cirrus` references after removing stale `*.Cirrus.*.dll-Help.xml` files.
- tools/Help scan result (2026-07-03): 0 remaining `Cirrus` references.
- tests/Cli.Tests scan result (2026-07-03): 60 references across 17 files.
- tests/Cli.Tests hotspots: `ExtensionAddPlannerTests.cs` (15), `ExtensionRemoveTests.cs` (7), `StripKnownExtensionPrefixTests.cs` (6), `RegistryLookupTests.cs` (6).
- src/Extensions scan result (2026-07-03): 0 remaining `Cirrus` references.
- src/Core scan result (2026-07-03): 15 references across 11 files.
- src/Core classification: mixed compatibility + stale naming/docs.
    - Candidate rename/update: `RegisterCirrusServices` method name/usages, README prose, and Cirrus wording in XML doc comments/examples.
- src/Core code-change pass result (2026-07-03): reduced from 15 hits/11 files to 0 hits/0 files.
- src/Core decision: removed legacy `Microsoft.Azure.Cirrus.Extensions.*` prefix handling in core assembly loading and short-name lookup; renamed DI registration helper to `RegisterAzclipsServices`.
- src/CLI scan result (2026-07-03): 12 references across 4 files.
- src/CLI classification: mixed compatibility + branding/env naming.
    - Compatibility-preserving logic: dual prefix handling and short-name extraction for legacy extension package ids (`Microsoft.Azure.Cirrus.Extensions.*`).
    - Candidate rename/update: appx manifest identity/display text and `CIRRUS_IN_AGENT_MODE` env var/comment wording in CLI startup fallback.
- src/CLI code-change pass result (2026-07-03): reduced from 12 hits/4 files to 0 hits/0 files.
- src/CLI decision: removed legacy `Microsoft.Azure.Cirrus.Extensions.*` prefix handling and aligned CLI tests to Azclips-only prefix behavior.
- tests/Scripts scan result (2026-07-03): 109 references across 7 files.
- tests/Scripts hotspots: `CLI/DeploymentCreate.sh` (27), `PS/DeploymentCreate.ps1` (26), `CLI/DeploymentWhatIf.sh` (19), `PS/DeploymentWhatIf.ps1` (18), `PS/rest.ps1` (15).
- tests/Scripts classification: mostly sample resource/deployment names prefixed with `cirrus-*`; includes command compatibility mentions like `Invoke-CirrusRestMethod` and a historical env-var comment (`CIRRUS_AGENT_DISABLED`).
- tests/Core.Extensions.Tests scan result (2026-07-03): 36 references across 5 files.
- tests/Core.Extensions.Tests hotspots: `FileExtensionRegistryStoreTests.cs` (11), `MultiNamespaceTests.cs` (11), `ExtensionHintTableTests.cs` (8).
- tests/Core.Extensions.Tests classification: mostly compatibility assertions for legacy package ids (`Microsoft.Azure.Cirrus.Extensions.*`) and temp path labels (`cirrus-*`).
- tests/Fixtures scan result (2026-07-03): 10 references across 4 files.
- tests/Fixtures hotspots: `SharedAlpha/ExtensionIndex.cs` (3), `SharedBeta/ExtensionIndex.cs` (3), `SharedAlpha/Cirrus.Tests.Fixtures.SharedAlpha.csproj` (2), `SharedBeta/Cirrus.Tests.Fixtures.SharedBeta.csproj` (2).
- tests/Fixtures classification: fixture assembly identity and namespace names still use `Cirrus.Tests.Fixtures.*` for test linkage compatibility.
- tests/Fixtures code-change pass result (2026-07-03): reduced from 10 hits/4 files to 0 hits/0 files.
- tests/Fixtures decision: renamed fixture projects, assembly identities, and fixture metadata from `Cirrus.Tests.Fixtures.*` to `Azclips.Tests.Fixtures.*`, and updated solution/test references accordingly.
- tests/Cmdlets.Tests scan result (2026-07-03): 0 remaining `Cirrus` references.
