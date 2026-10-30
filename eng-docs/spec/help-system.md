# Design Spec: Help System

Two parallel markdown corpora drive three surfaces: CLI help authored under `src/CLI/Help/Docs/<ext>/…/<command>.md` (nested — the filesystem mirrors the command tree) powers `az --help` and a JSON catalog for external tooling; PowerShell help authored under `src/PS/<Module>/help/<Cmdlet>.md` is compiled to MAML XML by PlatyPS and served by `Get-Help`. Authoring guide: [eng-docs/howto/help.md](../howto/help.md).

---

## 1. Goals

| Goal                   | Rationale                                                                                                              |
| ---------------------- | ---------------------------------------------------------------------------------------------------------------------- |
| One file per command   | Each command/cmdlet has a single authoring source; runtime, MAML, and CI tooling consume it without duplication.       |
| Tree-aware CLI help    | `az --help`, `az group --help`, `az group create --help` each produce meaningfully different output.                   |
| Native PowerShell help | `Get-Help <Cmdlet> -Full` is served by standard MAML XML packaged with the module.                                     |
| Cold-path safe         | CLI help renders without loading execution code or instantiating processors.                                           |
| Authorable by humans   | Authors edit `.md` files next to the extension; no recompile to iterate on prose. Codegen may interject in the future. |
| Mechanically validated | Every example is parsed and checked against live `ExtensionIndex` metadata at build time; stale help fails CI.         |
| AI-augmentable         | The same markdown corpus is consumed by the agent's prompt feed.                                                       |

---

## 2. Architecture at a Glance

```
src/CLI/Help/Docs/<ext>/**/*.md ◄── CLI corpus (az --help) — nested tree, index.md per group
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
              Azclips.Help.Tool (dotnet run)
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

## 3. Help Routing via Discovery (Runtime Model)

Help resolution in Azclips is **discovery-backed**. The runtime uses the same `DiscoverySession` that powers extension dispatch and listing to route help requests. This ensures help is always in sync with what's actually installed.

### 3.1 Root Help (`az --help`)

When a user runs `az --help`:

1. **Run discovery pass** — open a discovery session (`IEngine.OpenDiscoverySession(...).RunAsync()`) to emit `List<DiscoveredExtensionInfo>` for all installed extensions and resolved versions.
2. **Collect verbs** — for each discovered extension, use the `Verbs` payload from `DiscoveredExtensionInfo` (e.g., `vm`, `disk`, `group`).
3. **Render roots** — Build a root help page listing all discovered verbs and their one-line summaries.
4. **Return to user** — `az --help` displays the collected namespace tree.

**Example**: If three extensions are discovered (`Resources` with `group`, `Compute` with `vm`/`disk`, `Accounts` with `login`), root help shows all four verbs grouped by extension.

### 3.2 Verb & Command Help (`az vm --help`, `az vm create --help`)

When a user runs help for a verb or command:

1. **Parse the path** — Extract the canonical command key (e.g., `vm` for root group, `vm create` for a command).
2. **Run discovery pass** — Enumerate all discovered extensions.
3. **Resolve ownership** — Walk the discovered verbs/commands to find which extension owns the requested path:
    - Check each discovered extension's `Verbs` list for the first segment.
    - If found, use the discovered command table (`DiscoveredExtensionInfo.Commands`) for metadata and path ownership.
    - If not found in any discovered extension, fall back to the hint table.
4. **Load help markdown** — Call `MarkdownCommandHelpProvider.Get(extensionName, commandKey)` to read and render the markdown.
5. **Return to user** — Display help for the resolved verb or command.

**Example**: User runs `az vm create --help`:

- Discover that `Compute` extension owns verb `vm`.
- Use `Compute`'s discovered `Commands` table to locate the `create` command.
- Read `src/CLI/Help/Docs/vm/create.md` and render it.

Lifecycle tags come from `ProcessorInfoAttribute.Tags`, independently of markdown.
The runtime adapter preserves them in detailed command-help records and command-list
summaries, including runnable root commands. Command listings use the existing
bracketed format (for example, `create [Preview]:`); standalone command-help formatting
does not change. Processors without lifecycle tags remain untagged.

### 3.3 Unknown Verbs & Hint Table Fallback

If a user requests help for a verb that **no discovered extension owns**:

1. **Check hint table** — Query the embedded verb→package hint table (built at compile time from first-party extension declarations).
2. **If found** — Surface a structured message: `"Verb 'foo' is provided by extension 'Package.Foo', which is not installed. Install it with: az extension add --name Foo"`.
3. **If not found** — Return a generic "unknown verb" error.

**Key point:** The hint table is **fallback-only**. It's used only when discovery finds no installed owner. This prevents stale verb-to-package mappings from masking newly installed extensions or leading users to the wrong package.

### 3.4 Consistency Across Surfaces

- **CLI** (`az --help`) — Uses discovery-backed routing (above).
- **PowerShell** (`Get-Help`) — Help is pre-compiled to MAML XML and packaged with the module, so no runtime discovery happens. However, at **authoring time**, the same markdown corpus is validated against discovery to ensure consistency.
- **Extension API** (`az extension list`) — Uses the same discovery output, overlaid with registry preferences (pins, install metadata).

---

## 4. Authoring Model

CLI help lives under `src/CLI/Help/Docs/<ext>/`; PowerShell help under `src/PS/<Module>/help/`. The CLI layout is **nested** — the filesystem mirrors the command tree:

| Path role                                     | File                           |
| --------------------------------------------- | ------------------------------ |
| Verb root (`az <verb>`)                       | `<ext>/index.md`               |
| Leaf command (`az <verb> <cmd>`)              | `<ext>/<cmd>.md`               |
| Intermediate sub-group (`az <verb> <sub>`)    | `<ext>/<sub>/index.md`         |
| Nested leaf (`az <verb> <sub> <cmd>`)         | `<ext>/<sub>/<cmd>.md`         |
| Deeper leaf (`az <verb> <sub1> <sub2> <cmd>`) | `<ext>/<sub1>/<sub2>/<cmd>.md` |

Groups and leaves are disambiguated by which file exists on disk — `MarkdownCommandHelpProvider.ResolveHelpFilePath` tries the leaf form first, then falls back to `<path>/index.md`. Intermediate `index.md` files are required by `HelpCompletenessValidator` for every prefix of every routed command key, so the tree cannot ship with blank summaries at any depth.

PowerShell files are named `<Verb-Noun>.md` (one per cmdlet), per PlatyPS convention.

Each file has YAML frontmatter (file-level metadata) followed by a markdown body (description + examples):

````markdown
---
summary: One-line description.
status: ga # ga | preview | deprecated | internal (default ga)
schema: 1.0.0 # frontmatter schema version
online version: # MSLearn URL (optional)
relatedLinks:
    - az logout
---

Description: Multi-paragraph explanation in the body.

### Login with subscription id (CLI)

```bash
az login --subscription-id 00000000-0000-0000-0000-000000000000
```

### Connect with subscription id (PowerShell)

```powershell
Connect-AzAccount -SubscriptionId "00000000-0000-0000-0000-000000000000"
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

At runtime the provider first tries the repo source markdown when `azclips.slnx` is reachable from `AppContext.BaseDirectory` — the dev-loop path, so authors editing `src/CLI/Help/Docs/<ext>/…/<key>.md` see results without rebuilding. When no repo root is reachable (shipped installs), it falls back to the packaged copy at `<AppContext.BaseDirectory>/help/<ext>/…/<key>.md` (or `<key>/index.md` for group nodes). There is no embedded-resource path today.

Markdown is hand-authored today. Codegen may interject at this layer in the future (e.g. emitting templates from `IExtensionIndex`); the on-disk contract downstream stays the same.

Full author workflow lives in [eng-docs/howto/help.md](../howto/help.md).

---

## 5. CLI Surface

`-h` / `--help` / `help` is intercepted **before** command execution. The adapter resolves the canonical command key (full path, space-delimited, lowercased) and calls `MarkdownCommandHelpProvider.Get(extensionName, commandKey)`, which reads the corresponding markdown file from disk, filters examples to `!IsPowerShell`, and renders.

Cold-path guarantees:

- No reflection over processor types on the help path.
- No processor instantiation.
- One synchronous file read per invocation; no caching today.

Tree shape mirrors azure-cli: `az --help` lists groups, `az group --help` lists subcommands, `az group create --help` shows full command help. Global arguments (`--help`, `--debug`, `--output`) are owned by the CLI host and surface on every page.

The runtime supports a source-tree fallback for repo contributors: if the packaged file at `<AppContext.BaseDirectory>/help/<ext>/<key>.md` is **missing**, the provider walks up from `AppContext.BaseDirectory` looking for `azclips.slnx` and reads the markdown straight from `src/CLI/Help/Docs/<ext>/`. The fallback only fires on absence — a stale copy in the build output will be served as-is, so the inner loop is still "edit `.md`, rebuild, see new help."

---

## 6. PowerShell Surface

PowerShell help is delivered the standard way: a `.dll-Help.xml` MAML file colocated with the cmdlet assembly. Built from the same markdown corpus:

1. `Azclips.Help.Tool` parses markdown for every command in a PS extension.
2. It emits `artifacts/help/powershell/Microsoft.Azure.Azclips.Cmdlets.<Ext>.dll-Help.xml`.
3. `build.ps1 -GenerateHelpArtifacts` copies the XML into `src/PS/<Module>/en-US/` (handles both `RootModule` and `NestedModules` manifests).

Result: `Get-Help <Cmdlet> -Full` works natively with no custom resolver. Examples preserve quoting and surface in `-Examples` mode.

---

## 7. Build-Time Validation Pipeline

`Azclips.Help.Tool` runs five validators in order, each emitting structured `ValidationIssue`s with a stable `ProblemId`. CI surfaces the tool via the `help-validate` job in [build-and-test.yml](../../.github/workflows/build-and-test.yml).

| Validator                          | Checks                                                                                                                                                                                                                                                                                                                                                    | Failure mode                              |
| ---------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ----------------------------------------- |
| `MarkdownStructureValidator`       | Frontmatter present + valid; `summary` and `schema` required; `status` (if set) is a valid enum value; body has description and at least one example; code fences balanced.                                                                                                                                                                               | Hard error.                               |
| `HelpCompletenessValidator`        | Every routed CLI command in `ExtensionIndex` has a corresponding markdown file under `src/CLI/Help/Docs/<verb>/`; warns on orphan markdown files.                                                                                                                                                                                                         | Hard error on missing; warning on orphan. |
| `ExampleSyntaxValidator`           | Every `az …` line tokenizes; first token resolves to a known group; remaining non-flag tokens resolve via `IExtensionIndex.GetCommandTable(CLI)`; every `--flag`/`-x` exists on the resolved command (or is global); values type-check against `ParameterMetadata.Type` and `AllowedValues`.                                                              | Hard error.                               |
| `PowerShellExampleSyntaxValidator` | Every `Verb-Noun` resolves via the reflected cmdlet index; every `-Parameter` resolves to a declared parameter or alias; common parameters are whitelisted; splatted commands skip parameter checks (PSL1–PSL8). Detects `TODO -` (`PS_TodoPlaceholder`), `<angle-bracket>` shapes (`PS_ExamplePlaceholder`), and PlatyPS `{{ Fill … }}` markers (PSL12). | Hard error.                               |
| `PsCompletenessValidator`          | Walks `src/PS/<Module>/<Module>.psd1`, parses `CmdletsToExport` via PowerShell AST, and verifies every exported cmdlet has a matching `help/<Verb-Noun>.md` (PSL9). Flags missing `help/` folders (PSL10, supersedes the per-cmdlet PSL9 cascade) and warns on orphaned help files (PSL11).                                                               | Hard error on missing; warning on orphan. |

**Placeholders.** `<angle-bracket>` shapes (`<rg>`, `<my-vm>`, `<string>`) are rejected by `CLI_ExamplePlaceholder` / `PS_ExamplePlaceholder` and must be replaced with realistic literals. Accepted escape hatches: shell/PowerShell variables (`$var`, `${var}`), splatted PowerShell hashtables (`Get-Foo @params`), and the all-zero Guid sentinel (`00000000-0000-0000-0000-000000000000`) for subscription/tenant/resource ids — the latter parses cleanly as a `Guid` so it satisfies CLI_L3 / PSL6 type-checks without leaking real tenant data.

All five validators run over every extension before the tool exits; errors are aggregated, grouped by `ProblemId`, and the tool returns non-zero if any error was collected (warnings alone still exit 0). Manifest emission for a given extension is skipped if that extension has completeness errors.

Tests live in [tools/Help/Azclips.Help.Tool.Tests/](../../tools/Help/Azclips.Help.Tool.Tests/).

---

## 8. Design Philosophy

**Markdown is the contract.** Authoring is one file per command; the CLI runtime reads it from the host's content output (with a repo fallback), PowerShell consumes the MAML built from the same source, and tooling validates and republishes it. There is no second source of truth — every surface derives from the same `.md` file at build time.

**Lenient at runtime, strict at build.** The runtime parser tolerates malformed markdown (returns partial content, never throws) so a typo in one help file never crashes the CLI. The build-time validators are unforgiving — every defect surfaces as a CI error with a precise file:line locator.

**Static over executable.** The PowerShell validator uses `System.Management.Automation.Language.Parser.ParseInput` — AST only, no runspace, no script execution. The CLI validator uses a small shell-aware tokenizer. Name and signature checks are static, and statics scale.

**Examples are shape-tested code.** Every example tokenizes against the live extension index, every flag is verified, every literal value is type-checked. Renaming a flag without updating examples is a build break, not a customer report. Note: this catches syntactic and structural drift only — a shape-valid example can still be semantically wrong (invalid name patterns, missing service-side prerequisites, unsupported region combinations). A future iteration may seed examples from commands that have passed integration tests, closing the semantic gap.

**Hand-rolled where the schema is tiny.** The frontmatter parser is ~150 lines, owns its error messages, and has zero NuGet surface. YamlDotNet is one swap away when the schema demands multi-line scalars or nested objects.

**Cosmetic vs structural is a sharp line.** The `(CLI)` / `(PowerShell)` H3 suffix is cosmetic — it exists for the human reader; the parser pivots on the fence language tag. Conflating the two would create two sources of truth for "which surface is this example for"; keeping them separate keeps the parser dumb and the conventions human-friendly.

**Ownership boundary.** Runtime owns help loading, surface filtering, and rendering. Tooling owns parsing-for-validation, manifest generation, and publishing. Both consume the same markdown, but they never share a parsing path — runtime stays lightweight; tooling can grow validators without touching the cold path.
