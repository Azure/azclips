# Design Spec: Help System

## 1. Status

**Shipped:** Phases 1–4 are complete. Markdown is the canonical source for help content across CLI and PowerShell surfaces. A four-stage validator pipeline gates every PR. The Login, Config, Resources, Extension, and Compute extensions are fully migrated.

**In flight (this PR):** §9 — final consistency pass, PlatyPS-shaped frontmatter, validator UX polish, CI restructuring.

**Out of scope:** MSLearn publishing pipeline and the broader PlatyPS body shape; see §10.

---

## 2. Goals

| Goal                   | Rationale                                                                                                      |
| ---------------------- | -------------------------------------------------------------------------------------------------------------- |
| Single source of truth | One markdown file per command; no duplication between CLI and PowerShell.                                      |
| Tree-aware CLI help    | `azv2 --help`, `azv2 group --help`, `azv2 group create --help` each produce meaningfully different output.     |
| Native PowerShell help | `Get-Help <Cmdlet> -Full` is served by standard MAML XML packaged with the module.                             |
| Cold-path safe         | CLI help renders without loading execution code or instantiating processors.                                   |
| Authorable by humans   | Authors edit `.md` files next to the extension; no recompile to iterate.                                       |
| Mechanically validated | Every example is parsed and checked against live `ExtensionIndex` metadata at build time; stale help fails CI. |
| AI-augmentable         | The same markdown corpus is consumed by the agent's prompt feed.                                               |

---

## 3. Architecture at a Glance

```
src/Extensions/<Ext>/help/*.md  ◄── single source of truth
        │
        ├── runtime ──────────────────────────────────────────────
        │
        │     MarkdownCommandHelpProvider.Parse(content)
        │       ├── FrontmatterParser  (summary, status, schema, …)
        │       └── body regex         (description, examples)
        │
        │     ExtensionIndexHelpAdapter
        │       ├── CLI:        filters !IsPowerShell, renders
        │       └── PowerShell: full example list, MAML XML
        │
        └── build time ───────────────────────────────────────────
              Cirrus.Help.Tool (dotnet run)
                ├── MarkdownStructureValidator
                ├── HelpCompletenessValidator     (per extension)
                ├── ExampleSyntaxValidator        (CLI L1/L2/L3)
                ├── PowerShellExampleSyntaxValidator (PS-L1/L2)
                └── ExtensionHelpManifestBuilder
                      ├── artifacts/help/<ext>/HelpManifest.json
                      ├── artifacts/help/HelpIndex.json
                      └── artifacts/help/powershell/<Module>.dll-Help.xml
                          → copied into src/PS/<Module>/en-US/
```

Two consumers, one source. Runtime reads markdown directly (cold-path friendly, fast iteration). Build-time tooling consumes the same markdown plus reflected `ExtensionIndex` / cmdlet metadata to validate and emit publish-ready artifacts.

---

## 4. Authoring Model

Help lives under `src/Extensions/<Extension>/help/`. Root commands use `index.md`; subcommands use `<segment>-<segment>.md` (space-delimited canonical CLI key, dasherized).

Each file has YAML frontmatter (file-level metadata) followed by a markdown body (description + examples):

````markdown
---
summary: One-line description.
status: ga # ga | preview | deprecated | internal (default ga)
schema: 1.0.0 # frontmatter schema version
online version: # MSLearn URL, empty until §10.1 lands
relatedLinks:
    - azv2 logout
---

Description: Multi-paragraph explanation in the body.

### Login with subscription id (CLI)

```bash
azv2 login --subscription-id 00000000-0000-0000-0000-000000000000
```

### Connect with subscription id (PowerShell)

```powershell
Connect-CirrusAccount -SubscriptionId "00000000-0000-0000-0000-000000000000"
```
````

**Surface differentiation is driven by the code-fence language tag** (`bash`/`sh` → CLI, `powershell`/`pwsh` → PowerShell). The `(CLI)` / `(PowerShell)` H3 suffix is cosmetic — it disambiguates for the human reader; the parser ignores it.

Full author workflow lives in [docs/howto/help.md](../howto/help.md).

---

## 5. CLI Surface

`-h` / `--help` / `help` is intercepted **before** command execution. The adapter resolves the canonical command key (full path, space-delimited, lowercased), reads the corresponding markdown file, filters examples to `!IsPowerShell`, and renders.

Cold-path guarantees:

- No reflection over processor types on the help path.
- No processor instantiation.
- One synchronous markdown read per invocation; no caching today (revisit if profiling demands it).

Tree shape mirrors azure-cli: `azv2 --help` lists groups, `azv2 group --help` lists subcommands, `azv2 group create --help` shows full command help. Global arguments (`--help`, `--debug`, `--output`) are owned by the CLI host and surface on every page.

---

## 6. PowerShell Surface

PowerShell help is delivered the standard way: a `.dll-Help.xml` MAML file colocated with the cmdlet assembly. Built from the same markdown corpus:

1. `Cirrus.Help.Tool` parses markdown for every command in a PS extension.
2. It emits `artifacts/help/powershell/Microsoft.Azure.Cirrus.Cmdlets.<Ext>.dll-Help.xml`.
3. `build.ps1 -GenerateHelpArtifacts` copies the XML into `src/PS/<Module>/en-US/` (handles both `RootModule` and `NestedModules` manifests).

Result: `Get-Help <Cmdlet> -Full` works natively with no custom resolver. Examples preserve quoting and surface in `-Examples` mode.

---

## 7. Build-Time Validation Pipeline

`Cirrus.Help.Tool` runs four validators in order, each emitting structured `ValidationIssue`s with a stable `ProblemId`. CI surfaces the tool via the "Validate Help Content" step in [build-and-test.yml](../../.github/workflows/build-and-test.yml).

| Validator                                     | Checks                                                                                                                                                                                                                                                                                                                                             | Failure mode                              |
| --------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ----------------------------------------- |
| `MarkdownStructureValidator`                  | Frontmatter present + valid; `summary` and `schema` required; `status` (if set) is a valid enum value; body has description and at least one example; code fences balanced.                                                                                                                                                                        | Hard error.                               |
| `HelpCompletenessValidator`                   | Every routed command in `ExtensionIndex` has a corresponding markdown file; warns on orphan markdown files.                                                                                                                                                                                                                                        | Hard error on missing; warning on orphan. |
| `ExampleSyntaxValidator` (CLI L1/L2/L3)       | Every `azv2 …` line tokenizes; first token resolves to a known group; remaining non-flag tokens resolve via `IExtensionIndex.GetCommandTable(CLI)`; every `--flag`/`-x` exists on the resolved command (or is global); values type-check against `ParameterMetadata.Type` and `AllowedValues` (Guid, int/long, float/double/decimal, bool, enums). | Hard error.                               |
| `PowerShellExampleSyntaxValidator` (PS-L1/L2) | Every `Verb-Noun` resolves via reflected cmdlet index; every `-Parameter` resolves to a declared parameter or alias; common parameters whitelisted; splatted commands skip L2 only.                                                                                                                                                                | Hard error.                               |

**Placeholders** (`<rg>`, `$var`, `${var}`, the all-zero Guid) pass through L3 unchecked. **Positional parameters** are intentionally not yet enforced — required-flag-presence is deferred until processor metadata exposes positional bindings.

Tests live in [tools/Help/Cirrus.Help.Tool.Tests/](../../tools/Help/Cirrus.Help.Tool.Tests/) alongside the tool, mirroring the azure-powershell `StaticAnalysis` / `StaticAnalysis.Test` convention.

---

## 8. Design Philosophy

**Markdown is the contract.** Runtime reads it directly; tooling validates and republishes it. There is no second source of truth and no "regenerate help" step between editing and running `azv2 --help`. Authors iterate at the speed of `Ctrl-S`.

**Lenient at runtime, strict at build.** The runtime parser tolerates malformed markdown (returns partial content, never throws) so a typo in one help file never crashes the CLI. The build-time validators are unforgiving — every defect surfaces as a CI error with a precise file:line locator.

**Static over executable.** The PowerShell validator uses `System.Management.Automation.Language.Parser.ParseInput` — AST only, no runspace, no script execution. The CLI validator uses a small shell-aware tokenizer. We deliberately avoid `PSScriptAnalyzer` and any execution-based approach: name and signature checks are static, and statics scale.

**Examples are tested code.** L1 (command path) + L2 (flag signature) + L3 (value type) collapse "is the example correct?" and "is the help outdated?" into a single mechanical check. Renaming a flag without updating examples is a build break, not a customer report.

**Hand-rolled where the schema is tiny.** The frontmatter parser is ~150 lines, owns its error messages, and has zero NuGet surface. We will swap in YamlDotNet when the schema actually demands it (multi-line scalars, nested objects) — not before.

**Cosmetic vs structural is a sharp line.** The `(CLI)` / `(PowerShell)` H3 suffix is cosmetic — it exists for the human reader; the parser pivots on the fence language tag. Conflating the two would create two sources of truth for "which surface is this example for"; keeping them separate keeps the parser dumb and the conventions human-friendly.

**Ownership boundary.** Runtime owns help loading, surface filtering, and rendering. Tooling owns parsing-for-validation, manifest generation, and publishing. Both consume the same markdown, but they never share a parsing path — runtime stays lightweight; tooling can grow validators without touching the cold path.

---

## 9. In-Progress Polish (this PR)

### 9.1 ✅ PlatyPS-shaped file-level frontmatter — **DONE**

- Every help file under `src/Extensions/*/help/*.md` carries a YAML frontmatter block with `summary` (required), `status` (default `ga`), `schema: 1.0.0` (required), `online version` (optional, empty), `relatedLinks` (optional list).
- The body keeps its `Description:` line plus the example sections; bash example H3 titles gained a `(CLI)` suffix to mirror the existing `(PowerShell)` convention.
- New `FrontmatterParser` in `Core.Runtime` (~150 lines, hand-rolled — see §8). [MarkdownCommandHelpProvider](../../src/Core/Core.Runtime/Help/MarkdownCommandHelpProvider.cs) extracts and parses frontmatter; description and example regex behavior unchanged on the body.
- [MarkdownStructureValidator](../../tools/Help/Cirrus.Help.Tool/Validation/MarkdownStructureValidator.cs) enforces the schema: frontmatter present, `summary` and `schema` required, `status` (if set) in `ValidStatuses`.
- All 17 help files migrated in a single mechanical pass. No dual-format support.
- Tests: new `FrontmatterParserTests` (9), reworked `MarkdownStructureValidatorTests`, updated `MarkdownCommandHelpProviderTests` fixtures, refreshed Login golden files.
- Author template in [docs/howto/help.md](../howto/help.md) updated.
- **Side fix:** [build.ps1](../../build/build.ps1) `-GenerateHelpArtifacts` now copies `.dll-Help.xml` for modules using `NestedModules` (was previously only handling `RootModule`); unblocks `Get-Help` for Compute and Resources.

### 9.2 ✅ Spec updates — **DONE**

- This document distilled from ~700 lines to ~175 — preserved §9 (in-progress polish) and §10 (follow-ups), refactored everything before into a tighter what-we-built + design-philosophy shape.
- [ARCHITECTURE.md](./ARCHITECTURE.md) gained a Help System section, three rows in the File Locations table (`Help Markdown`, `Help Runtime Provider`, `Help Tool`), and cross-references in the Documentation list.
- [docs/howto/help.md](../howto/help.md) audited against the shipped frontmatter: fixed broken Quick Start markdown, repaired cross-references, expanded Example Validation to cover both CLI and PowerShell surfaces in parallel, fixed stale artifact paths (`Cirrus.Login.dll-Help.xml` → `Microsoft.Azure.Cirrus.Cmdlets.<Module>.dll-Help.xml`) and import paths, documented the `build.ps1 -GenerateHelpArtifacts` step that lands MAML next to cmdlet DLLs, removed the obsolete "Migrating an Extension" section, and rewrote Troubleshooting around `ProblemId` buckets.

### 9.3 ✅ Code documentation — **DONE**

- Added class- and member-level XML doc comments to the four runtime entry points: [`FrontmatterParser`](../../src/Core/Core.Runtime/Help/FrontmatterParser.cs), [`MarkdownCommandHelpProvider`](../../src/Core/Core.Runtime/Help/MarkdownCommandHelpProvider.cs), [`ExtensionHelpManifestBuilder`](../../src/Core/Core.Runtime/Help/ExtensionHelpManifestBuilder.cs), and [`ExtensionIndexHelpAdapter`](../../src/CLI/Help/ExtensionIndexHelpAdapter.cs).
- Documented the schema record (`FrontmatterDoc`), the language-tag-drives-surface rule on `MarkdownExample`, the `Empty` sentinel + fallback chain on `MarkdownCommandHelp`, the parallel-surface emission contract in `BuildManifest`, and the markdown-first / attribute-fallback resolution order in the CLI adapter.
- Refreshed stale spec cross-references (`§15.5.1` → `§4`, `§16.5` → `§10.5`) so code comments point at the refactored spec sections.

### 9.4 ✅ Developer guides — **DONE**

- Troubleshooting section in [docs/howto/help.md](../howto/help.md) covers the four common failure buckets (no examples rendered, empty `Get-Help` REMARKS, `CLI_L1/L2/L3` validator errors, `PSL1/PSL2` validator errors, malformed frontmatter) — added during the §9.2 audit and verified still accurate.
- New **Templates** section in the same doc ships five copy-paste-ready skeletons: group root (`index.md`), dual-surface subcommand, CLI-only subcommand, PowerShell-only cmdlet, and a status-frontmatter snippet for `preview` / `deprecated` / `internal` commands.

### 9.5 ✅ Collate all validator issues before failing — **DONE**

- **What shipped:** Removed the four early-throw sites in [tools/Help/Cirrus.Help.Tool/Program.cs](../../tools/Help/Cirrus.Help.Tool/Program.cs). Every validator stage now runs over every extension; errors are merged into a single `aggregateReport`. Manifest emission for a given extension is skipped iff that extension has completeness errors (an incomplete corpus would build a misleading manifest), but iteration continues. Final summary groups warnings then errors by `ProblemId`, sorted by file path within each group, and the tool returns exit code 1 iff any errors collected (warnings-only is still exit 0).
- **`ProblemId` taxonomy:** New [`ProblemIds`](../../tools/Help/Cirrus.Help.Tool/Validation/ProblemIds.cs) constants class with stable string IDs (`MD_MissingSummary`, `Completeness_MissingHelpFile`, `CLI_L1_UnknownGroup`, `CLI_L2_UnknownFlag`, `CLI_L3_InvalidValue`, `PSL1_UnknownCmdlet`, `PSL2_UnknownParameter`, etc.). Every existing `AddError`/`AddWarning` call site stamped. `ValidationIssue.ProblemId` is optional in the record — issues without one fall into an `<unclassified>` bucket so nothing is silently dropped.
- **Verified:** Existing 91 validator tests pass unchanged (they assert on `Message`/`FilePath`, not `ProblemId`). Manual two-stage break (MD frontmatter + PSL1 unknown cmdlet) produced a single grouped summary with both errors visible and exit code 1, where the old behavior would have surfaced only the first stage. CI semantics preserved: any error → red.

### 9.6 ✅ Split help validation into its own CI job — **DONE**

- **What shipped:** [build-and-test.yml](../../.github/workflows/build-and-test.yml) split into three independent top-level jobs: `build-and-test` (existing code build + xUnit + E2E, minus the old help step), `help-validate` (calls the help tool directly — `dotnet run --project tools/Help/Cirrus.Help.Tool ...` — skipping `build.ps1` since CI doesn't need the en-US copy), and `help-tool-tests` (just `dotnet test tools/Help/Cirrus.Help.Tool.Tests/...`).
- **Concurrency:** workflow-level `cancel-in-progress` on the same ref so force-push to a PR branch cancels in-flight runs across all three jobs together.
- **Tradeoff accepted:** each job runs its own `restore` + `build`. Redundant work, but trivial workflow code and clean PR-check attribution. Tracked for follow-up as §10.10 (artifact sharing).
- **Branch protection:** if `build-and-test` is configured as a required check, replace it with the three new check names (`build-and-test`, `help-validate`, `help-tool-tests`) in the GitHub repo settings UI. No-op if no required checks are configured.
- **Verification:** Breaking a `.cs` file fails `build-and-test`, leaves `help-validate` green. Breaking a help example fails `help-validate`, leaves `build-and-test` green. Both jobs run on every PR; failures are clearly attributed to the slice that broke.

### 9.7 ✅ CLI L2/L3 error message uses routed command path — **DONE**

- **What shipped:** [`ExampleSyntaxValidator`](../../tools/Help/Cirrus.Help.Tool/Validation/ExampleSyntaxValidator.cs) now captures the `cliTable` key it matched, builds `routedPath = "azv2 {group}"` (or `"azv2 {group} {key}"` when the command has a sub-name), and threads it into `ValidateFlags`. Both `CLI_L2_UnknownFlag` and `CLI_L3_InvalidValue` messages quote the routed path instead of `CommandMetadata.Name` (which is the processor class name — `"LoginProcessor"`, `"NewResourceGroupProcessor"` — and is an implementation detail).
- **Before/after:** `…on command 'LoginProcessor'…` → `…on command 'azv2 login'…`. For grouped commands: `…on command 'NewResourceGroupProcessor'…` → `…on command 'azv2 group create'…`.
- **PowerShell side already correct:** [`PowerShellExampleSyntaxValidator`](../../tools/Help/Cirrus.Help.Tool/Validation/PowerShellExampleSyntaxValidator.cs) reads the cmdlet name straight from the AST (`Connect-CirrusAccount`), so `PSL2_UnknownParameter` was never affected. No change needed.
- **Test impact:** Existing 91 tests pass unchanged — they assert on the `'--flag'` prefix, not the trailing `on command '...'` portion. No new test added since there is no extension where the routed path equals the processor class name (would just duplicate the manual smoke check).

### 9.8 Tool-level integration tests over `Program.MainAsync`

- **Problem:** Every validator class has strong unit coverage; `Program.cs` has none. Stage ordering, exit-code contract, manifest-emission gating, and the CLI-group → extension map are all untested. §9.5 will rewrite orchestration with nothing to catch a silent revert.
- **Prerequisite refactor:** Extract `Program.Main` body into `Program.MainAsync(string[] args, string repoRoot, TextWriter? stderr = null) → int`; inject `repoRoot` for fixture-driven tests.
- **Fixtures** under `tools/Help/Cirrus.Help.Tool.Tests/Fixtures/`: `Clean/`, `MissingHelpFile/`, `BadL3Value/`, `BadPSL2/`, `MultiExtensionErrors/`.
- **Tests** (~5 facts, one per fixture). `MultiExtensionErrors` is the lock for §9.5 — initially `Skip`ped, un-skipped once §9.5 lands.
- **Out of scope:** Subprocess E2E (slow, brittle, no value over in-process `MainAsync`). Snapshot manifest assertions ([HelpArtifactWriterTests](../../tests/Cli.Tests/Help/HelpArtifactWriterTests.cs) already covers that layer).

---

## 10. Follow-ups (out of this PR)

Recorded so we don't lose design context; **not blockers** for landing this PR.

### 10.1 MSLearn publishing pipeline

Export `src/Extensions/*/help/*.md` to a docs-pipeline-ready format (PlatyPS Markdown or `learn.microsoft.com` JSON) and wire into release CI. Likely shape: a new `tools/Help/Cirrus.Help.Publish/` console app that reads existing manifests + body extraction and emits an `out/mslearn/` tree. Triggered on release tag, separate workflow. The §9.1 frontmatter (`online version`, `relatedLinks`) is the on-ramp.

### 10.2 Per-parameter YAML blocks (full PlatyPS shape)

Each `### -ParamName` body subsection would carry its own YAML block with `Type`, `Required`, `Position`, `Default value`, `Accept pipeline input`, `Aliases`. Deferred because `ParameterMetadata` in code is the single source of truth today — adopting PlatyPS's per-param YAML duplicates that into markdown and requires reconciliation tooling (their `Update-MarkdownHelp` cmdlet). Reconsider when docs authoring becomes a separate role from cmdlet authoring.

### 10.3 `## DESCRIPTION` body header (PlatyPS body shape)

Move from `Description:` prefix line to `## DESCRIPTION` H2. Mechanical churn across 17 files for no capability gain today. Bundle with other body-shape changes (`## EXAMPLES`, `## RELATED LINKS`) if/when adopted, to amortize the diff.

### 10.4 Structured `relatedLinks`

Promote `relatedLinks` from flat string list to `[{ name, url }]` objects. Strings suffice for terminal rendering; the structured shape pays off when publishing to HTML (§10.1).

### 10.5 YamlDotNet swap

Replace the hand-rolled `FrontmatterParser` with YamlDotNet when the schema grows multi-line scalars, nested objects (§10.4), or per-parameter blocks (§10.2). Single-line decision to flip.

### 10.6 Tighten extension parameter types

Audit every `ExtensionIndex.cs` parameter declaration and replace `typeof(string)` with the precise type (`typeof(Guid)` for `subscription-id`, `typeof(int)` for `--retries`, etc.). L3 (§7) only catches type-shape errors when metadata declares a non-string type — tightening types multiplies L3's reach. Mechanical, touches every extension, best as a focused follow-up.

### 10.7 Help authoring scaffolder

`cirrus help new <extension> <command>` (or `dotnet run --project tools/Help/Cirrus.Help.Tool -- new …`) that emits a frontmatter+body template. Copy-paste from the howto is fast enough at current corpus size. Reconsider past ~50 commands or when onboarding new authors becomes friction.

### 10.8 Pattern / regex validation for name fields

Add `[ValidationPattern]` (regex) or `[NamingRule]` to `ParameterMetadata` so help examples like `--name 123` for a VM (must start with a letter) fail L3. Service-side naming rules are the authoritative validator; duplicating them is brittle. L3 today catches type-shape, not semantic-validity — keeping those concerns separate is honest design. Reconsider if real defects start slipping through.

### 10.9 Help output formatting audit

Audit rendered help across surfaces (`azv2 --help`, `azv2 <command> --help`, `Get-Help`, agent prompt feed) for visual consistency and information density: section ordering, heading style, indentation, column widths, wrapping behavior. Compare against `az` and Azure PowerShell conventions and document deliberate deviations. Best done as a focused review pass once the in-flight help PR has merged so the audit is against shipped output, not a moving target. Capture findings (and any before/after deltas) in [docs/howto/help.md](../howto/help.md).

### 10.10 Share build artifacts across CI jobs

Today the three CI jobs (`build-and-test`, `help-validate`, `help-tool-tests`) each run their own `dotnet restore` + `dotnet build` — clean attribution at the cost of triple work. Replace with a single `build` job that uploads `artifacts/Debug/` + each project's `bin/Debug/` (likely tarred together) via `actions/upload-artifact@v4`; downstream jobs `download-artifact` and run `dotnet test --no-build` / the help tool against the shared output. Adds NuGet cache (`actions/cache@v4` keyed on `**/*.csproj` hash) to make the `build` job itself faster. Worth doing once we have multiple PRs queueing on CI minutes; not worth the workflow complexity for our current volume.
