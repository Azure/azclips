# TEMP PR Plan - Cirrus Reference Sweep

## Scope

Track and remove remaining `Cirrus` references across selected directories.

## Directory Checklist

- [x] `src/PS` (owner: Daniel, references: 0, files: 0)
- [x] `tools/Help` (owner: Daniel, references: 0, files: 0)
- [ ] `tests/Cli.Tests` (owner: Daniel, references: 60, files: 17)
- [x] `src/Extensions` (owner: Daniel, references: 0, files: 0)
- [ ] `src/Core` (owner: Daniel, references: 15, files: 11)
- [ ] `src/CLI` (owner: Daniel, references: 9, files: 25)
- [ ] `tests/Scripts` (owner: Daniel, references: 7, files: 109)

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
	- Compatibility-preserving logic: dual package prefixes in assembly loading and registry lookup (`Microsoft.Azure.Cirrus.Extensions.*` retained intentionally for legacy installs).
	- Candidate rename/update: `RegisterCirrusServices` method name/usages, README prose, and Cirrus wording in XML doc comments/examples.
