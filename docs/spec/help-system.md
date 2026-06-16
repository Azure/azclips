# Design Spec: Help System

Two parallel markdown corpora drive three surfaces: CLI help authored under `src/CLI/Help/Docs/<ext>/<command>.md` powers `azv2 --help` and a JSON catalog for external tooling; PowerShell help authored under `src/PS/<Module>/help/<Cmdlet>.md` is compiled to MAML XML by PlatyPS and served by `Get-Help`. Authoring guide: [docs/howto/help.md](../howto/help.md).

---

## 1. Goals

| Goal                   | Rationale                                                                                                              |
| ---------------------- | ---------------------------------------------------------------------------------------------------------------------- |
| One file per command   | Each command/cmdlet has a single authoring source; runtime, MAML, and CI tooling consume it without duplication.       |
| Tree-aware CLI help    | `azv2 --help`, `azv2 group --help`, `azv2 group create --help` each produce meaningfully different output.             |
| Native PowerShell help | `Get-Help <Cmdlet> -Full` is served by standard MAML XML packaged with the module.                                     |
| Cold-path safe         | CLI help renders without loading execution code or instantiating processors.                                           |
| Authorable by humans   | Authors edit `.md` files next to the extension; no recompile to iterate on prose. Codegen may interject in the future. |
| Mechanically validated | Every example is parsed and checked against live `ExtensionIndex` metadata at build time; stale help fails CI.         |
| AI-augmentable         | The same markdown corpus is consumed by the agent's prompt feed.                                                       |

---

## 2. Architecture at a Glance

```
src/CLI/Help/Docs/<ext>/*.md    ◄── CLI corpus (azv2 --help)
src/PS/<Module>/help/*.md       ◄── PowerShell corpus (Get-Help, MAML via PlatyPS)
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
                ├── HelpCompletenessValidator           (CLI: per extension)
                ├── ExampleSyntaxValidator              (CLI examples)
                ├── PowerShellExampleSyntaxValidator    (PS examples)
                ├── PsCompletenessValidator             (PS: per module manifest)
                └── ExtensionHelpManifestBuilder
                      ├── artifacts/help/<ext>/HelpManifest.json
                      ├── artifacts/help/HelpIndex.json
                      └── artifacts/help/powershell/<Module>.dll-Help.xml
                          → copied into src/PS/<Module>/en-US/
```

Two consumers, one source. The CLI runtime reads markdown from the host's content output (`<AppContext.BaseDirectory>/help/<ext>/<key>.md`, with a repo-source fallback for the dev loop) — cold-path friendly, no reflection, no embedded-resource path. Build-time tooling consumes the same markdown plus reflected `ExtensionIndex` / cmdlet metadata to validate and emit publish-ready artifacts.

---

## 3. Authoring Model

CLI help lives under `src/CLI/Help/Docs/<ext>/`; PowerShell help under `src/PS/<Module>/help/`. CLI root commands use `index.md` and subcommands use `<segment>-<segment>.md` (space-delimited canonical CLI key, dasherized). PowerShell files are named `<Verb-Noun>.md` (one per cmdlet), per PlatyPS convention.

Each file has YAML frontmatter (file-level metadata) followed by a markdown body (description + examples):

````markdown
---
summary: One-line description.
status: ga # ga | preview | deprecated | internal (default ga)
schema: 1.0.0 # frontmatter schema version
online version: # MSLearn URL (optional)
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

The CLI host project copies the markdown corpus to its build output as content:

```xml
<ItemGroup>
    <Content Include="Help\**\*.md">
        <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </Content>
</ItemGroup>
```

At runtime the provider resolves `<AppContext.BaseDirectory>/help/<ext>/<key>.md` first, with a dev-loop fallback that walks up to `cirrus.slnx` and reads `src/CLI/Help/Docs/<ext>/<key>.md` directly. There is no embedded-resource path today.

Markdown is hand-authored today. Codegen may interject at this layer in the future (e.g. emitting templates from `IExtensionIndex`); the on-disk contract downstream stays the same.

Full author workflow lives in [docs/howto/help.md](../howto/help.md).

---

## 4. CLI Surface

`-h` / `--help` / `help` is intercepted **before** command execution. The adapter resolves the canonical command key (full path, space-delimited, lowercased) and calls `MarkdownCommandHelpProvider.Get(extensionName, commandKey)`, which reads the corresponding markdown file from disk, filters examples to `!IsPowerShell`, and renders.

Cold-path guarantees:

- No reflection over processor types on the help path.
- No processor instantiation.
- One synchronous file read per invocation; no caching today.

Tree shape mirrors azure-cli: `azv2 --help` lists groups, `azv2 group --help` lists subcommands, `azv2 group create --help` shows full command help. Global arguments (`--help`, `--debug`, `--output`) are owned by the CLI host and surface on every page.

The runtime supports a source-tree fallback for repo contributors: if the packaged file at `<AppContext.BaseDirectory>/help/<ext>/<key>.md` is **missing**, the provider walks up from `AppContext.BaseDirectory` looking for `cirrus.slnx` and reads the markdown straight from `src/CLI/Help/Docs/<ext>/`. The fallback only fires on absence — a stale copy in the build output will be served as-is, so the inner loop is still "edit `.md`, rebuild, see new help."

---

## 5. PowerShell Surface

PowerShell help is delivered the standard way: a `.dll-Help.xml` MAML file colocated with the cmdlet assembly. Built from the same markdown corpus:

1. `Cirrus.Help.Tool` parses markdown for every command in a PS extension.
2. It emits `artifacts/help/powershell/Microsoft.Azure.Cirrus.Cmdlets.<Ext>.dll-Help.xml`.
3. `build.ps1 -GenerateHelpArtifacts` copies the XML into `src/PS/<Module>/en-US/` (handles both `RootModule` and `NestedModules` manifests).

Result: `Get-Help <Cmdlet> -Full` works natively with no custom resolver. Examples preserve quoting and surface in `-Examples` mode.

---

## 6. Build-Time Validation Pipeline

`Cirrus.Help.Tool` runs five validators in order, each emitting structured `ValidationIssue`s with a stable `ProblemId`. CI surfaces the tool via the `help-validate` job in [build-and-test.yml](../../.github/workflows/build-and-test.yml).

| Validator                          | Checks                                                                                                                                                                                                                                                                                                                                                    | Failure mode                              |
| ---------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ----------------------------------------- |
| `MarkdownStructureValidator`       | Frontmatter present + valid; `summary` and `schema` required; `status` (if set) is a valid enum value; body has description and at least one example; code fences balanced.                                                                                                                                                                               | Hard error.                               |
| `HelpCompletenessValidator`        | Every routed CLI command in `ExtensionIndex` has a corresponding markdown file under `src/CLI/Help/Docs/<verb>/`; warns on orphan markdown files.                                                                                                                                                                                                         | Hard error on missing; warning on orphan. |
| `ExampleSyntaxValidator`           | Every `azv2 …` line tokenizes; first token resolves to a known group; remaining non-flag tokens resolve via `IExtensionIndex.GetCommandTable(CLI)`; every `--flag`/`-x` exists on the resolved command (or is global); values type-check against `ParameterMetadata.Type` and `AllowedValues`.                                                            | Hard error.                               |
| `PowerShellExampleSyntaxValidator` | Every `Verb-Noun` resolves via the reflected cmdlet index; every `-Parameter` resolves to a declared parameter or alias; common parameters are whitelisted; splatted commands skip parameter checks (PSL1–PSL8). Detects `TODO -` (`PS_TodoPlaceholder`), `<angle-bracket>` shapes (`PS_ExamplePlaceholder`), and PlatyPS `{{ Fill … }}` markers (PSL12). | Hard error.                               |
| `PsCompletenessValidator`          | Walks `src/PS/<Module>/<Module>.psd1`, parses `CmdletsToExport` via PowerShell AST, and verifies every exported cmdlet has a matching `help/<Verb-Noun>.md` (PSL9). Flags missing `help/` folders (PSL10, supersedes the per-cmdlet PSL9 cascade) and warns on orphaned help files (PSL11).                                                               | Hard error on missing; warning on orphan. |

**Placeholders** (`<rg>`, `$var`, `${var}`, the all-zero Guid) pass through example value-checking unchecked.

All five validators run over every extension before the tool exits; errors are aggregated, grouped by `ProblemId`, and the tool returns non-zero if any error was collected (warnings alone still exit 0). Manifest emission for a given extension is skipped if that extension has completeness errors.

Tests live in [tools/Help/Cirrus.Help.Tool.Tests/](../../tools/Help/Cirrus.Help.Tool.Tests/).

---

## 7. Design Philosophy

**Markdown is the contract.** Authoring is one file per command; the CLI runtime reads it from the host's content output (with a repo fallback), PowerShell consumes the MAML built from the same source, and tooling validates and republishes it. There is no second source of truth — every surface derives from the same `.md` file at build time.

**Lenient at runtime, strict at build.** The runtime parser tolerates malformed markdown (returns partial content, never throws) so a typo in one help file never crashes the CLI. The build-time validators are unforgiving — every defect surfaces as a CI error with a precise file:line locator.

**Static over executable.** The PowerShell validator uses `System.Management.Automation.Language.Parser.ParseInput` — AST only, no runspace, no script execution. The CLI validator uses a small shell-aware tokenizer. Name and signature checks are static, and statics scale.

**Examples are shape-tested code.** Every example tokenizes against the live extension index, every flag is verified, every literal value is type-checked. Renaming a flag without updating examples is a build break, not a customer report. Note: this catches syntactic and structural drift only — a shape-valid example can still be semantically wrong (invalid name patterns, missing service-side prerequisites, unsupported region combinations). A future iteration may seed examples from commands that have passed integration tests, closing the semantic gap.

**Hand-rolled where the schema is tiny.** The frontmatter parser is ~150 lines, owns its error messages, and has zero NuGet surface. YamlDotNet is one swap away when the schema demands multi-line scalars or nested objects.

**Cosmetic vs structural is a sharp line.** The `(CLI)` / `(PowerShell)` H3 suffix is cosmetic — it exists for the human reader; the parser pivots on the fence language tag. Conflating the two would create two sources of truth for "which surface is this example for"; keeping them separate keeps the parser dumb and the conventions human-friendly.

**Ownership boundary.** Runtime owns help loading, surface filtering, and rendering. Tooling owns parsing-for-validation, manifest generation, and publishing. Both consume the same markdown, but they never share a parsing path — runtime stays lightweight; tooling can grow validators without touching the cold path.
