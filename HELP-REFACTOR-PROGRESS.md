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

- [x] Add `build/Install-BuildDependencies.ps1` (PlatyPS pinned to 0.14.2)
- [x] Wire into `build.ps1` `-GenerateHelpArtifacts` block
- [x] README note about pinned version

### Generate PS markdown

- [x] `New-MarkdownHelp` per module → `src/PS/<Module>/help/` (helper at `build/New-PSHelpMarkdown.ps1`)
- [x] Hand-fill synopses/descriptions/examples (ported from old extension help)
- [x] Commit `src/PS/<Module>/help/*.md` (Compute 6, Config 5, Login 3, Resources 2)

### MAML emission

- [x] `New-ExternalHelp` per module → `src/PS/<Module>/en-US/` (writes directly into module folder, no separate `artifacts/help/powershell/` staging)
- [x] Verify filenames match `Get-Help` expectations
- [x] Confirm copy-into-`en-US` step still works (now done by PlatyPS itself)

### Strip PS paths from `Cirrus.Help.Tool`

- [x] Remove PS MAML writer (`WritePowerShellHelpXml` + `--skip-pwsh-xml` flag deleted; PlatyPS now owns MAML)
- [ ] ~~Remove PS example validation~~ — **kept** per locked plan ("Keep everything, repoint paths"); validator now scans `src/PS/<Module>/help/`
- [x] Tool emits CLI-only artifacts (manifests + HelpIndex.json; PlatyPS emits MAML)

### Phase 2 verification

- [x] `dotnet build cirrus.slnx` clean
- [x] `Help validation: no issues.` (validator now scans new PS path)
- [x] All tests pass (91 CLI + 92 help tool)
- [x] `Import-Module .\artifacts\Debug\PS\<Mod>\<Mod>.psd1`
- [x] `Get-Help <Cmdlet> -Full` returns full content for one cmdlet per module
- [x] `Get-Help <Cmdlet> -Examples` returns examples

---

## Phase 3 — CLI markdown scaffolder

- [x] Add `--scaffold <extension>` (sibling tool inside `Cirrus.Help.Tool`) — walks `GetCommandTable(ClientType.CLI)`, emits templates
- [x] Idempotent (skip existing files)
- [x] Document in `docs/howto/help.md`

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
