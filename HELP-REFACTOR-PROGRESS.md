# Help System Refactor — Progress Tracker

**Temporary file** — delete before merging this PR.

Tracks execution of the bifurcation refactor agreed in the design call.

---

## Decisions locked in

- **Sequencing:** all phases in this PR.
- **CLI markdown layout:** extension-keyed (`src/CLI/help/<extension>/<command>.md`).
- **PlatyPS:** install-on-demand, version-pinned, in `build/Install-BuildDependencies.ps1` invoked from `build.ps1`.
- **Help tool naming:** `Cirrus.Help.Tool` becomes CLI-only (rename optional, can defer).
- **Agent artifact shape (psName + powerShell examples):** **deferred to follow-up PR.**

---

## Phase 1 — Bifurcate authoring + relocate types

### Directory layout

- [x] Create `src/CLI/help/<extension>/` for: compute, config, extension, login, resources
- [x] Move CLI content from `src/Extensions/<Ext>/help/*.md` → `src/CLI/help/<ext>/`
- [x] Strip `(CLI)` / `(PowerShell)` heading suffixes (CLI-only files now)
- [x] Add `summary` frontmatter to each `index.md` carrying old `CliRootSummary` text

### Remove old surface

- [x] Delete `help/` folder from each `src/Extensions/<Ext>/`
- [x] Remove `<EmbeddedResource Include="help/**/*.md" />` from each extension `.csproj`
- [x] Delete `CliRootSummary` from each `ExtensionIndex.cs`
- [x] Delete `CliRootSummary` from `IExtensionIndex` interface

### Move types out of Core.Abstractions / Core.Runtime

- [x] Move `Core.Abstractions/Models/HelpContracts.cs` → `Core.Cli/Help/`, ns `Core.Cli.Help`
- [x] Move `Core.Runtime/Help/ExtensionHelpManifestBuilder.cs` → `Core.Cli/Help/`
- [x] Move `Core.Runtime/Help/MarkdownCommandHelpProvider.cs` → `Core.Cli/Help/`
- [x] Move `Core.Runtime/Help/FrontmatterParser.cs` → `Core.Cli/Help/`
- [x] Update all consumers (help tool, CLI program, tests) to new namespaces

### Update markdown lookup

- [x] `MarkdownCommandHelpProvider` reads from filesystem (`<base>/help/<ext>/<key>.md`), not embedded resources
- [x] Build step copies `src/CLI/help/` → `artifacts/<Config>/Core.Cli/help/`
- [x] `ExtensionIndexHelpAdapter` sources group summary from `index.md` frontmatter (replaces removed `CliRootSummary`)

### Phase 1 verification

- [x] `dotnet build cirrus.slnx` clean
- [x] `azv2 vm --help`, `azv2 group --help`, `azv2 login --help` render with correct summary
- [x] `azv2 vm create --help` renders examples and parameters
- [x] `Help validation: no issues.` from help tool

---

## Phase 2 — PlatyPS for PowerShell

### Build infrastructure

- [ ] Add `build/Install-BuildDependencies.ps1` (PlatyPS pinned)
- [ ] Wire into `build.ps1` `-GenerateHelpArtifacts` block
- [ ] README note about pinned version

### Generate PS markdown

- [ ] `New-MarkdownHelp` per module → `src/PS/<Module>/help/`
- [ ] Hand-fill synopses/descriptions/examples (port from old extension help)
- [ ] Commit `src/PS/<Module>/help/*.md`

### MAML emission

- [ ] `New-ExternalHelp` per module → `artifacts/help/powershell/`
- [ ] Verify filenames match `Get-Help` expectations
- [ ] Confirm copy-into-`en-US` step still works

### Strip PS paths from `Cirrus.Help.Tool`

- [ ] Remove PS MAML writer
- [ ] Remove PS example validation
- [ ] Tool emits CLI-only artifacts

### Phase 2 verification

- [ ] `Import-Module .\artifacts\Debug\PS\<Mod>\<Mod>.psd1`
- [ ] `Get-Help <Cmdlet> -Full` returns full content for one cmdlet per module
- [ ] `Get-Help <Cmdlet> -Examples` returns examples

---

## Phase 3 — CLI markdown scaffolder

- [ ] Add `--scaffold <extension>` (or sibling tool) — walks `s_cliTable`, emits templates
- [ ] Idempotent (skip existing files)
- [ ] Document in `docs/howto/help.md`

---

## Tests

- [ ] Update `tests/Cli.Tests/HelpArtifactWriterTests.cs` for new fixtures + CLI-only shape
- [ ] Update `tools/Help/Cirrus.Help.Tool.Tests` for new authoring location
- [ ] Smoke test: `Get-Help` end-to-end for one cmdlet per module
- [ ] Smoke test: `azv2 <group> --help` + `azv2 <group> <cmd> --help` for one per extension

---

## Documentation

- [ ] Update `docs/howto/help.md` — new authoring locations, PlatyPS workflow, CLI scaffolder
- [ ] Update `docs/spec/help-system.md` — corrected diagram, `Core.Cli.Help` namespace
- [ ] Update `AGENTS.md` if it references old help paths

---

## Final checklist

- [ ] All phases complete
- [ ] All tests passing
- [ ] Delete this file (`HELP-REFACTOR-PROGRESS.md`)
