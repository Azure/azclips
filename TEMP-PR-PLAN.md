# TEMP PR Plan - Cirrus Reference Sweep

## Scope

Track and remove remaining `Cirrus` references across selected directories.

## Directory Checklist

- [x] `src/PS` (owner: Daniel, references: 0, files: 0)
- [x] `tools/Help` (owner: Daniel, references: 0, files: 0)
- [ ] `tests/Cli.Tests` (owner: Daniel, references: 21, files: 86)
- [ ] `src/Extensions` (owner: Daniel, references: 17, files: 71)
- [ ] `src/Core` (owner: Daniel, references: 15, files: 36)
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
