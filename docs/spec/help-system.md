# Design Spec: Help System

## Status: In Progress (Phase 1 Complete, Phase 2+ Planned)

> This document describes the help system for Cirrus being actively built. **Phase 1 (Shared Help Model) is complete and validated with the Login extension end-to-end.**
> Phase 2 (CLI Runtime Help integration) and later phases are in progress.
>
> **Current implementation snapshot:**
>
> - Markdown help files under `src/Extensions/{Extension}/help/` with YAML frontmatter are the authoritative source.
> - `MarkdownCommandHelpProvider` loads and parses markdown (YAML + structured examples) at runtime.
> - CLI help adapter filters examples by surface section (CLI vs PowerShell).
> - Manifest builder splits examples by surface (cli/powerShell arrays in JSON).
> - Build script integration: `.\build\build.ps1 -GenerateHelpArtifacts` exports help JSON and PowerShell XML.
> - See [docs/howto/help.md](../howto/help.md) for author workflow.

---

## 1. Goals

| Goal                                  | Rationale                                                                                                                                   |
| ------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------- |
| **Single source of truth**            | Help content lives in one place per command/parameter; no duplication between CLI and PowerShell surfaces                                   |
| **Tree-aware CLI help**               | `azv2 --help`, `azv2 vm --help`, `azv2 vm start --help` each produce meaningfully different output, mirroring azure-cli UX                  |
| **Flat PowerShell help**              | `Get-Help New-ResourceGroup -Full` follows standard MAML conventions; cmdlet discovery via `Get-Command`                                    |
| **Fast at runtime**                   | CLI help resolves from lightweight extension-owned metadata without loading command execution code on the cold path                         |
| **No markdown dependency at runtime** | CLI runtime help does not read markdown files or generated artifacts; help comes from extension metadata                                    |
| **Authorable by humans**              | Descriptions and examples live in extension metadata close to command definitions; a developer can add help without deep renderer knowledge |
| **AI-augmentable**                    | The agent can answer "how do I…" using the same help data that drives the terminal output                                                   |

---

## 2. Current State (Implementation Progress)

| Area                       | Current state                                                                                | Status                                                                                                |
| -------------------------- | -------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------- |
| Markdown help authoring    | Help files under `src/Extensions/{Extension}/help/{command}.md`; markdown is source of truth | ✅ **Implemented and validated** (Login extension fully migrated; Config/Resources/Extension pending) |
| Example language tagging   | Examples in markdown tagged by language (` ```bash ` or ` ```powershell `)                   | ✅ **Implemented** (CLI filters `!IsPowerShell`, PowerShell gets full list)                           |
| Runtime markdown loading   | `MarkdownCommandHelpProvider` parses markdown files at runtime                               | ✅ **Implemented** (resolves from repo root; caches parsed content)                                   |
| CLI help adapter           | `ExtensionIndexHelpAdapter` calls markdown provider first, falls back to attributes          | ✅ **Implemented** (filters CLI examples, passes all to PowerShell)                                   |
| Manifest builder           | `ExtensionHelpManifestBuilder` splits examples by surface (cli/powerShell arrays)            | ✅ **Implemented** (used during artifact generation)                                                  |
| Build-time artifact export | `.\build\build.ps1 -GenerateHelpArtifacts` generates JSON manifests and PowerShell XML       | ✅ **Implemented** (integrated into build script)                                                     |
| CLI help rendering         | `HelpCell`, `HelpLine`, `MultiGroupHelpTable` for terminal output                            | ⏳ **Partially wired** (templates exist; runtime interception pending Phase 2)                        |
| CLI group/tree help        | Nested command tree help (root → group → command)                                            | ⏳ **Pending Phase 2** (templates exist; tree resolution pending)                                     |
| PowerShell runtime help    | Native PowerShell `Get-Help` integration with pre-generated MAML XML                         | ⏳ **Pending Phase 3** (artifacts generated; PowerShell packaging pending)                            |
| Parameter-level help       | `[ArgumentInfo(Description = "...")]` on processor properties                                | ✅ **Already exists** (used in help display)                                                          |
| Command descriptions       | `Summary` and `Description` fields in markdown files                                         | ✅ **Implemented** (parsed from markdown; can fall back to attributes)                                |
| Examples                   | CLI and PowerShell examples in markdown code blocks                                          | ✅ **Implemented** (Login fully authored; Config/Resources/Extension pending)                         |
| Metadata duplication       | Descriptions removed from code attributes; single source in markdown                         | ✅ **Done for Login** (Config/Resources/Extension pending cleanup)                                    |
| CI enforcement             | Linting/validation for required help content                                                 | ⏳ **Planned Phase 4** (currently no pre-commit validation)                                           |

---

## 3. Single Source of Truth: Markdown Help Files

**Design change from original spec:** Rather than sourcing help from code attributes, all help content originates from **markdown files** under `src/Extensions/{Extension}/help/`. This approach is more ergonomic for authors and keeps help content readable and reviewable.

Markdown files are the canonical source. At runtime, `MarkdownCommandHelpProvider` loads and parses them. At build time, optional tooling projects markdown into JSON manifests and PowerShell XML for packaging and publishing workflows.

### 3.1 Markdown file structure

Help files live in extension-owned directories:

```
src/Extensions/Login/help/
├── index.md          # Root login command
├── logout.md         # Logout subcommand
└── show.md           # Show context subcommand

src/Extensions/Config/help/
├── index.md          # Root config command
└── (subcommands as needed)
```

For **root commands** (like `login`), use `help/index.md`.  
For **subcommands** (like `login logout`), use `help/{subcommand-name}.md`.

### 3.2 Markdown format (authored by humans)

Each help file contains YAML frontmatter for metadata, followed by structured example sections:

````yaml
---
summary: "One-line description of the command"
description: |
  Multi-paragraph explanation of what the command does,
  when to use it, and any important notes. Can span multiple lines.
---

## Examples

### CLI

#### Example Title
```bash
azv2 login --subscription-id 00000000-0000-0000-0000-000000000000
````

### PowerShell

#### Example Title

```powershell
Connect-CirrusAccount -SubscriptionId "00000000-0000-0000-0000-000000000000"
```

````

**YAML frontmatter fields:**
- `summary` (required): One-line command description (shown in group help, help listings)
- `description` (required): Full multi-paragraph explanation
- `tags` (optional, Phase 5+): `[Preview]`, `[Deprecated]`, `[Experimental]`
- `status` (optional, Phase 5+): `Preview`, `Experimental`, `Deprecated`
- `relatedLinks` (optional, Phase 5+): List of related command names or URLs

**Example sections:**
- `## Examples` — parent heading
- `### CLI` — CLI-specific examples (shown in `azv2 ... --help`)
- `### PowerShell` — PowerShell-specific examples (shown in `Get-Help`)
- Each example has a descriptive subheading (`#### Example Title`) followed by a code fence with language tag (`bash` or `powershell`)

### 3.3 Runtime parsing: `MarkdownCommandHelpProvider`

File: [src/Core/Core.Runtime/Help/MarkdownCommandHelpProvider.cs](../../src/Core/Core.Runtime/Help/MarkdownCommandHelpProvider.cs)

Responsibilities:
- Resolves markdown files from `src/Extensions/{extensionName}/help/{commandKey}.md`
- Parses YAML frontmatter (`summary`, `description`, `tags`, etc.)
- Extracts organized example sections (CLI vs PowerShell subsections)
- Parses code blocks with titles and language tags
- Tags examples: `IsPowerShell = true` if section is PowerShell, else `false` (CLI)
- Returns `MarkdownCommandHelp` record with parsed content

Caching: Parsed content is cached per process to avoid re-reading/re-parsing the same file.

### 3.4 Surface-specific filtering

**CLI path** (`azv2 ... --help`):
- Calls `MarkdownCommandHelpProvider.Get(extensionName, commandKey)`
- Filters examples: `examples.Where(e => !e.IsPowerShell)`
- Renders CLI-only examples

**PowerShell path** (`Get-Help` and artifacts):
- Calls `MarkdownCommandHelpProvider.Get(extensionName, commandKey)`
- Gets full list of examples (both CLI and PowerShell)
- PowerShell XML artifact includes all examples

### 3.5 Fallback to code attributes (for compatibility)

If markdown file is not found or lacks `summary` / `description` in YAML frontmatter, the system falls back to:
- `[ProcessorInfo]` `Summary` / `Description` attributes on processor classes
- `[ArgumentInfo]` `Description` on processor properties

This allows gradual migration: new extensions use YAML frontmatter markdown while legacy commands fall back to code attributes.

---

## 4. Build-Time and Tooling: From Markdown to Artifacts

### 4.1 Runtime help sourcing (no file dependencies)

**This has been reconsidered from the original spec.** Runtime CLI help **now reads markdown files**, which is simpler and more ergonomic than sourcing from code attributes.

Runtime flow:
1. CLI detects `--help` argument before command execution
2. Calls `ExtensionIndexHelpAdapter.GetSummary/GetDescription/GetExamples(extensionName, commandKey)`
3. Adapter calls `MarkdownCommandHelpProvider.Get(extensionName, commandKey)` (reads markdown from repo)
4. Adapter filters examples by surface (CLI-only) and renders help
5. CLI exits without executing command code

Why markdown at runtime?
- **Simpler for authors:** humans write `.md` files, not code attributes
- **Better discoverability:** help is in a dedicated folder, not scattered across processor classes
- **Easier maintenance:** one place to update examples per command
- **Faster iteration:** no code recompile needed to test help changes (just rebuild to regenerate artifacts)

### 4.2 Build-time artifact generation (optional)

File: [build/build.ps1](../../build/build.ps1) with `-GenerateHelpArtifacts` flag
Tool: [tools/Help/Cirrus.Help.Tool/Program.cs](../../tools/Help/Cirrus.Help.Tool/Program.cs)

When you run:
```powershell
.\build\build.ps1 -GenerateHelpArtifacts -BuildConfiguration Debug
````

The build script:

1. Runs normal `dotnet build`
2. Invokes `Cirrus.Help.Tool`, which:
    - Loads extension assemblies from `artifacts/Debug/`
    - For each extension, calls `ExtensionHelpManifestBuilder.BuildManifest()`
    - Reads markdown files and parses them via `MarkdownCommandHelpProvider`
    - Splits examples by surface: `IsPowerShell` flag determines cli/powerShell array
    - Emits output to `artifacts/help/`:
        - `{extension}/HelpManifest.json` (per-extension command metadata + split examples)
        - `HelpIndex.json` (cross-extension command index)
        - `powershell/{Module}.dll-Help.xml` (for PowerShell help packaging)

### 4.3 Artifact outputs

| Output                                   | Purpose                                                                          |
| ---------------------------------------- | -------------------------------------------------------------------------------- |
| `artifacts/help/{ext}/HelpManifest.json` | Machine-readable command + parameter metadata with cli/powerShell example arrays |
| `artifacts/help/HelpIndex.json`          | Cross-extension command index for CLI discovery/searching                        |
| `artifacts/help/powershell/{Module}.xml` | PowerShell MAML XML for `Get-Help` display and PowerShell package shipping       |

These artifacts are **optional** at runtime. CLI help reads markdown directly. Artifacts are for:

- Publishing workflows (MSLearn documentation)
- PowerShell module packaging (include XML in shipped module)
- Snapshot testing (verify no help content regressions)
- CI validation (ensure help metadata is complete)

### 4.4 Ownership boundary

| Owner   | Responsibility                                                                              |
| ------- | ------------------------------------------------------------------------------------------- |
| Runtime | Reading markdown files; filtering examples by surface; rendering help in CLI and PowerShell |
| Tooling | Parsing markdown; validating completeness; generating manifests/JSON/XML for publishing     |

Both use the same markdown source of truth, but they own different concerns.

---

## 5. CLI Help Surface

### 5.1 Help trigger

Help is triggered by `-h` / `--help` at any position in the argument list, or by the `help` subcommand:

```
azv2 --help
azv2 group --help
azv2 group create --help
azv2 help group create
```

Help is rendered **before** command execution (cold-path safe).

### 5.2 Rendering flow

When user invokes `azv2 login --help`:

1. CLI intercepts `--help` before command execution
2. Calls `ExtensionIndexHelpAdapter.GetSummary/Description/Examples("login", "")`
3. Adapter calls `MarkdownCommandHelpProvider.Get("login", "")`
4. Provider reads `src/Extensions/Login/help/index.md` and parses it
5. Adapter filters to CLI-only examples (`examples.Where(e => !e.IsPowerShell)`)
6. CLI renderer formats and displays help
7. Process exits (no command execution)

### 5.3 Root-level help (`azv2 --help`)

```
azv2 - Azure management CLI with AI fallback

Usage:
    azv2 [group] [command] [arguments...] [--help]
    azv2 "<natural language request>"

Groups:
    login     : Authenticate to Azure.
    group     : Manage resource groups.
    config    : Manage Cirrus configuration.
    extension : Manage Cirrus extensions.

Global Arguments:
    --help -h : Show this help message and exit.
    --debug   : Increase logging verbosity.
    --output  : Output format. Allowed: json, table, none. Default: table.

Learn more: https://aka.ms/cirrus
```

### 5.4 Group-level help (`azv2 group --help`)

```
Group
    azv2 group : Manage resource groups.

Commands:
    create : Create a resource group.
    delete : Delete a resource group.
    list   : List resource groups in a subscription.
    show   : Show a resource group.

Global Arguments:
    --help -h : Show this help message and exit.
```

### 5.5 Command-level help (`azv2 login --help`)

**Actual implementation (Login example):**

```
Command
    azv2 login : Authenticate to Azure and set the current account context.

Arguments
    --subscription-id [Required] : Azure Subscription Id.
    --tenant-id : Tenant ID to login to.
    --use-device-code : Use device code flow for authentication.

Examples
    Login with subscription id
        azv2 login --subscription-id 00000000-0000-0000-0000-000000000000
    Login with device code
        azv2 login --subscription-id 00000000-0000-0000-0000-000000000000 --use-device-code
```

Note: CLI-only examples are shown (PowerShell examples are filtered out).

### 5.6 Rendering implementation

File: [src/CLI/Help/CliHelpRenderer.cs](../../src/CLI/Help/CliHelpRenderer.cs) (or equivalent)

Responsibilities:

- Take `CommandMetadata` or parsed help content
- Format for terminal width
- Render grouped sections (Arguments, Examples)
- Handle word wrapping

Existing support:

- `HelpCell`, `HelpLine`, `MultiGroupHelpTable` classes for table layouts
- Terminal width detection

### 5.7 Canonical command key for nested CLI tree

Each command uses a canonical key:

- Lower-case
- Space-delimited segments
- Full path from group root to command leaf

Examples:

- `login` (root command)
- `login logout` (subcommand)
- `group create` (root command in group)
- `group role assignment create` (nested subcommand)

Lookup in markdown:

- Root commands: `src/Extensions/{Extension}/help/index.md`
- Subcommands: `src/Extensions/{Extension}/help/{subcommand-name}.md`

### 5.8 Runtime lifecycle and cold-path constraints

The CLI help path is:

1. Parse arguments and detect `-h` / `--help` / `help` **before** command execution
2. Call help adapter with extension name and command key
3. Adapter calls `MarkdownCommandHelpProvider.Get(extensionName, commandKey)`
4. Provider reads and parses markdown file (cached)
5. Adapter filters examples by surface (CLI-only: `!e.IsPowerShell`)
6. CLI renderer formats and displays help
7. Exit without creating command processor instances

Cold-path constraints:

- ❌ No runtime reflection over processor types to render help
- ❌ No processor instantiation on the help path
- ✅ Markdown files are read and cached (lightweight operation)
- ✅ Help metadata may be cached for process lifetime
- ✅ No dependency on generated JSON manifests or indexes

### 5.9 Global arguments

Global arguments (`--help`, `--debug`, `--output`) are defined once in the CLI host and shown on every help page. Extensions do not re-declare them.

---

## 6. PowerShell Help Surface (Phase 3+)

### 6.1 Approach: Native PowerShell Help at Runtime

PowerShell has two standard help delivery mechanisms:

| Mechanism                               | Description                                                                     | When to use                                              |
| --------------------------------------- | ------------------------------------------------------------------------------- | -------------------------------------------------------- |
| **`HelpMessage` in `[Parameter()]`**    | Inline; shown with `-?` but not `Get-Help -Full`                                | Quick inline hint; can be auto-generated from markdown   |
| **External MAML XML** (`.dll-Help.xml`) | Full Get-Help support: description, syntax, parameters, examples, related links | Required for `Get-Help -Full`, `-Examples`, `-Parameter` |

**Current state (Phase 3 pending):**

- Runtime help: PowerShell continues to use native cmdlet metadata and `Get-Help`
- Build-time artifacts: `.\build\build.ps1 -GenerateHelpArtifacts` produces MAML XML from markdown
- Packaging: When distributing PowerShell modules, include generated MAML in module folder

**Future enhancement:**

- Add `HelpMessage` to generated parameter metadata (optional, for inline help)
- Emit complete MAML XML with examples, related links, parameter descriptions

### 6.2 PowerShell help artifact generation

The help tool generates `artifacts/help/powershell/{Module}.dll-Help.xml` for each PowerShell module.

Example output structure:

```xml
<?xml version="1.0" encoding="utf-8"?>
<helpItems schema="maml" xmlns="http://msh.vtech.microsoft.com/maml/2004/07">
  <command:command xmlns:command="http://schemas.microsoft.com/maml/dev/command/2004/10">
    <command:details>
      <command:name>Connect-CirrusAccount</command:name>
      <maml:description>
        <maml:para>Log in to your Azure subscription to authenticate...</maml:para>
      </maml:description>
    </command:details>
    <command:syntax>
      <command:syntaxItem>
        <maml:name>Connect-CirrusAccount</maml:name>
        <command:parameter required="true" globbing="false" pipelineInput="false" position="named">
          <maml:name>SubscriptionId</maml:name>
          <maml:description>
            <maml:para>Azure Subscription ID.</maml:para>
          </maml:description>
        </command:parameter>
      </command:syntaxItem>
    </command:syntax>
    <command:examples>
      <command:example>
        <maml:title>--- Example 1: Connect with subscription ID ---</maml:title>
        <dev:code>Connect-CirrusAccount -SubscriptionId "00000000-0000-0000-0000-000000000000"</dev:code>
      </command:example>
    </command:examples>
  </command:command>
</helpItems>
```

### 6.3 `Get-Help` output (from generated MAML)

```
NAME
    Connect-CirrusAccount

SYNTAX
    Connect-CirrusAccount -SubscriptionId <string>

DESCRIPTION
    Log in to your Azure subscription to authenticate Cirrus CLI and PowerShell commands.

PARAMETERS
    -SubscriptionId <string>
        Azure Subscription ID. Defaults to the active subscription.

        Required?                    true
        Position?                    Named
        Accept pipeline input?       false

EXAMPLES
    Example 1: Connect with subscription ID
        Connect-CirrusAccount -SubscriptionId "00000000-0000-0000-0000-000000000000"

RELATED LINKS
    https://aka.ms/cirrus
```

---

## 7. Centralization: Single Source of Truth (Achieved)

**Before:** Descriptions were scattered across code attributes (`[ProcessorInfo]`, `[ArgumentInfo]`) and duplicated in static `ExtensionIndex` tables for PS and CLI.

**After:** Descriptions live **only** in markdown files. Code attributes are optional fallback only.

| Concern                | Before                                                               | After                                                               |
| ---------------------- | -------------------------------------------------------------------- | ------------------------------------------------------------------- |
| Command descriptions   | `[ProcessorInfo]` attribute on processor class                       | `Summary:` and `Description:` in markdown file                      |
| Parameter descriptions | `[ArgumentInfo]` attribute on processor property                     | `Summary:` and `Description:` in markdown file (parameters inherit) |
| CLI examples           | Code attribute or manual text                                        | Markdown code blocks tagged with `bash` language fence              |
| PowerShell examples    | None                                                                 | Markdown code blocks tagged with `powershell` language fence        |
| Source-of-truth        | Scattered across code (sync issues, drift)                           | Centralized markdown files (single author location)                 |
| Duplication            | PS `CommandMetadata` and CLI `CommandMetadata` both had descriptions | Single markdown file; both surfaces reference same source           |
| Update workflow        | Edit attribute → recompile → regenerate manifests → ship new DLL     | Edit markdown → optionally regenerate artifacts → commit            |

**Design flow (actual implementation):**

````
Markdown help files with YAML frontmatter (src/Extensions/{Extension}/help/*.md)
    │
    ├─► MarkdownCommandHelpProvider (runtime: parses YAML + examples)
    │   │
    │   ├─► CLI adapter (filters to CLI-only examples)
    │   │    └─► azv2 ... --help (shows CLI help with CLI examples)
    │   │
    │   └─► PowerShell path (gets all examples)
    │        └─► Get-Help (shows all examples in MAML)
    │
    └─► Build-time help tool (optional: generates artifacts)
        │
        ├─► HelpManifest.json (machine-readable metadata + split examples)
        ├─► HelpIndex.json (cross-extension command index)
        └─► PowerShell/*.dll-Help.xml (MAML for Get-Help packaging)

---

## 8. Performance Considerations

| Concern                                               | Mitigation                                                                                                      |
| ----------------------------------------------------- | --------------------------------------------------------------------------------------------------------------- |
| Cold-start help latency                               | CLI intercepts `--help` before command execution; reads markdown + renders (no assembly loading)                |
| Markdown file I/O on every help lookup                | `MarkdownCommandHelpProvider` caches parsed content per process; repeated lookups are O(1)                       |
| Loading execution assemblies just to show help        | ✅ Achieved: help is resolved before extension loading                                                           |
| Markdown parsing cost                                 | Minimal: simple regex-based parsing; cached in memory                                                            |
| Tooling artifact size                                 | Optional; not part of runtime. JSON/XML artifacts are for publishing, not consumed at help render time           |
| PowerShell help startup latency                       | Pre-generated MAML XML is included in module; `Get-Help` reads pre-built XML (no runtime generation)            |

---

## 9. AI Agent Integration

The AI agent (natural language fallback) can leverage the same help markdown data:

- Agent system prompt can be seeded with a command table extracted from markdown files
- Agent can reference real examples from markdown to suggest runnable commands
- Future: `HelpTool` function in Azure OpenAI lets agent query help during conversation

**Implementation status:**
- ⏳ Agent integration pending (Phase 4+)
- Markdown data structure supports agent consumption

---

## 10. Example Authoring

Examples are authored **by humans** in markdown code blocks organized by surface (CLI vs PowerShell). Each section has:
1. A surface subsection heading: `### CLI` or `### PowerShell`
2. Example subheadings: `#### Example Title`
3. A code fence with language tag: ` ```bash ` or ` ```powershell `
4. The actual command(s) to execute

**Example from Login help:**

```yaml
---
summary: "Authenticate to Azure and set the current account context"
description: |
  Log in to your Azure subscription using device code, subscription ID, or tenant ID.
---

## Examples

### CLI

#### Login with subscription ID
```bash
azv2 login --subscription-id 00000000-0000-0000-0000-000000000000
```

#### Login with device code
```bash
azv2 login --subscription-id 00000000-0000-0000-0000-000000000000 --use-device-code
```

### PowerShell

#### Connect with subscription ID
```powershell
Connect-CirrusAccount -SubscriptionId "00000000-0000-0000-0000-000000000000"
```
```

**AI-assisted examples (future, Phase 4+):**

For commands with no human-authored examples, a build-time AI step (opt-in) can suggest examples using Azure OpenAI. Suggested examples are:
1. Reviewed by a developer
2. Edited into the markdown file
3. Committed to the repository

This keeps examples human-reviewed while reducing authoring burden.

---

## 11. Tags / Status Markers

Status markers (Preview, Deprecated, Experimental) can be added to commands via code attributes or markdown metadata.

**Future enhancement (Phase 3+):**

```csharp
[ProcessorInfo(Tags = new[] { HelpTag.Preview })]
public class MyProcessor { ... }
````

Or in markdown:

```markdown
Summary: Create a resource group [Preview]
```

Tag rendering in help:

- `[Preview]` → shown in group help, no warning on use
- `[Deprecated]` → shown in group help, warning printed on use
- `[Experimental]` → shown in group help, warning printed on use

---

## 12. CI Enforcement (Phase 3)

Build-time validation checks:

| Rule                                          | Severity | Check                                                            |
| --------------------------------------------- | -------- | ---------------------------------------------------------------- |
| YAML frontmatter is valid                     | Error    | Build fails if YAML is malformed or missing                      |
| Markdown missing `summary` field in YAML      | Error    | Build fails if help file has no summary                          |
| Markdown missing `description` field in YAML  | Error    | Build fails if help file has no description                      |
| Markdown missing example sections             | Error    | Build fails if help file has no examples (CLI or PowerShell)     |
| Malformed markdown code fences                | Error    | Build fails if code block delimiters don't match                 |
| Routed command has no corresponding help file | Error    | Build fails if ExtensionIndex command has no markdown help       |
| Orphaned help files                           | Warning  | Build warns if markdown file isn't referenced by any command     |
| Parameter has no description                  | Warning  | Build warns if `[ArgumentInfo]` missing `Description` (optional) |

**Implementation:**

- ✅ Phase 1: Basic markdown structure validation in help tool
- ⏳ Phase 3: Stricter CI checks via build-time analyzer
- ⏳ Phase 5 (Subtask 1): YAML frontmatter validation

---

## 13. MSLearn Publishing (Phase 5+)

When Cirrus reaches a release cadence:

1. Release pipeline runs help export tool: `tools/Help/Cirrus.Help.Tool` with `--export-markdown` flag
2. Tool reads all markdown help files and generates per-command reference files into `artifacts/help/markdown/`
3. Optional: markdown files are published to MSLearn via integration with `azure-docs-cli` or `azure-docs-cirrus` repo
4. Same markdown source used for both runtime help and published reference docs (no duplication)

---

## 14. Design Decisions (Actual)

The following design decisions have been **made and validated** in the actual implementation:

| Area                           | Decision                                                                                               | Rationale                                                                                                             |
| ------------------------------ | ------------------------------------------------------------------------------------------------------ | --------------------------------------------------------------------------------------------------------------------- |
| **Help source of truth**       | Markdown files under `src/Extensions/{Extension}/help/` are the authoritative source.                  | More ergonomic for authors; easier to maintain; leverages familiar .md format; examples easier to write and review.   |
| **Markdown metadata format**   | YAML frontmatter for structured metadata (summary, description, tags, status).                         | Explicit structure; scalable for future enhancements; industry-standard format; easier to parse and validate.         |
| **Example organization**       | Examples organized in clear CLI/PowerShell subsections (`## Examples → ### CLI / ### PowerShell`)      | Clear surface differentiation; human-readable; structured parsing; scalable to more surfaces.                         |
| **Runtime markdown reading**   | Runtime CLI help reads markdown files directly (not JSON artifacts).                                   | Eliminates dependency on pre-generated manifests; allows fast iteration (change markdown, rebuild artifacts).         |
| **Surface-specific filtering** | CLI shows only CLI examples; PowerShell gets all examples (both CLI and PowerShell).                   | Each surface shows examples appropriate to its context; single markdown source stays clean and comprehensive.         |
| **Fallback to attributes**     | If markdown not found, system falls back to `[ProcessorInfo]` / `[ArgumentInfo]` attributes.           | Allows gradual migration; new extensions use markdown while legacy commands continue to work.                         |
| **Artifact generation**        | Optional build-time step (`-GenerateHelpArtifacts`); artifacts are for publishing, not runtime.        | Keeps runtime simple and fast; artifacts generated on-demand for specific workflows (packaging, CI validation).       |
| **Markdown validation**        | Build-time validators check YAML structure and required fields (summary, description, examples).       | Catches authoring errors early; ensures all commands have adequate help coverage; explicit format enforcement.        |
| **PowerShell help**            | PowerShell uses pre-generated MAML XML for `Get-Help` (generated from markdown at build time).         | Native PowerShell integration; no runtime custom help resolution; integrates with standard PowerShell help ecosystem. |
| **Ownership split**            | Runtime owns help loading and filtering; tooling owns artifact generation, validation, and publishing. | Clear separation of concerns; runtime stays lightweight; tooling can evolve independently.                            |
| **Extension metadata**         | Help lives in separate `help/` folder per extension, not scattered across processor code.              | Centralized location; easier to audit; avoids spreading help content across multiple files.                           |

---

## 15. Implementation Plan

### 15.1 ✅ Phase 1: Shared Help Model — COMPLETE

**Objective:** Establish markdown as the canonical help source and validate markdown → runtime flow.

**Delivered:**

1. ✅ Markdown help files as source of truth (`src/Extensions/{Extension}/help/`)
2. ✅ `MarkdownCommandHelpProvider` to parse and cache markdown at runtime
3. ✅ Example language tagging (bash/powershell) for surface differentiation
4. ✅ CLI adapter filters examples by surface (`!e.IsPowerShell` for CLI)
5. ✅ Manifest builder splits examples into cli/powerShell arrays
6. ✅ Build script integration (`.\build\build.ps1 -GenerateHelpArtifacts`)
7. ✅ Help artifact generation (JSON manifests, PowerShell XML)
8. ✅ Login extension fully migrated to markdown help (3 help files)
9. ✅ Tests passing (62/66, 4 skipped) with snapshot tests for artifacts
10. ✅ Author workflow documented in [docs/howto/help.md](../howto/help.md)

**Validation:** Login extension works end-to-end. `dotnet .\artifacts\Debug\Core.Cli\azv2.dll login --help` renders markdown help correctly with CLI-only examples.

---

### 15.2 ✅ Phase 2: Remaining Extension Migrations (Complete)

**Objective:** Extend markdown help pattern to Config, Resources, Extension extensions.

**Subtasks (completed in order):**

1. ✅ **Remove remaining embedded Summary/Description/Examples metadata**
    - Clean up Config, Resources, Extension `ExtensionIndex.cs` entries
    - Remove `[Summary]`/`[Description]`/`[Examples]` attributes from processors
    - Verify tests still pass (should use markdown fallback)

2. ✅ **Migrate Config extension to markdown help**
    - Create `src/Extensions/Config/help/` folder
    - Author markdown files for all Config commands
    - Remove from code attributes
    - Update test goldens if needed

3. ✅ **Migrate Resources extension to markdown help**
    - Create `src/Extensions/Resources/help/` folder
    - Author markdown files for all Resources commands
    - Remove from code attributes
    - Update test goldens if needed

4. ✅ **Migrate Extension extension to markdown help**
    - Create `src/Extensions/Extension/help/` folder
    - Author markdown files for extension management commands
    - Remove from code attributes
    - Update test goldens if needed

**Exit criteria (met):**

- All extensions have markdown help files for all commands
- No Summary/Description/Examples in code metadata for any extension
- Tests pass with markdown-sourced help for all extensions
- Artifact generation includes all extensions with correct examples

---

### 15.3 ⏳ Phase 3: Build-Time Validation and Linting

**Objective:** Add stricter enforcement that required help content exists and is well-formed.

**Subtasks:**

1. **Help markdown structure validation**
    - Check all help files have `Summary:` field (required)
    - Check all help files have `Description:` field (required)
    - Check all help files have at least one example block (required)
    - Check code fence syntax is correct (matching ``` delimiters)
    - Fail build if validation fails

2. **Help completeness checking**
    - For each command in `ExtensionIndex`, ensure corresponding markdown file exists
    - For each markdown file, verify it's referenced by a routed command
    - Warn on orphaned help files
    - Fail build if routed command has no help

3. **CI integration**
    - Wire help validation into PR pipeline CI (GitHub Actions or equivalent)
    - Add build step: `.\build\build.ps1 -GenerateHelpArtifacts -BuildConfiguration Release`
    - Validation fails the PR if:
        - Any help file missing `Summary:`, `Description:`, or examples
        - Any routed command lacks a corresponding help file
        - Any code fence syntax is malformed
    - Validation warns (does not fail PR) if:
        - Orphaned help files exist (can be merged with follow-up cleanup)
    - Output clear error messages with file paths and remediation steps
    - Consider Roslyn analyzer or `dotnet tool` in future for pre-commit local opt-in

**Exit criteria:**

- Build/PR pipeline fails if required help fields are missing
- CI prevents merging PRs that add commands without help
- Clear error messages guide developers to fixes
- Help validation runs as part of standard CI/CD pipeline

---

### 15.4 ⏳ Phase 4: Test Expansion and Edge Cases

**Objective:** Expand tests for malformed markdown and help coverage edge cases.

**Subtasks:**

1. **Malformed markdown handling**
    - Test missing `Summary:` field (fallback to attributes or error message)
    - Test missing `Description:` field
    - Test unpaired code fence delimiters
    - Test unknown language tags in code fences
    - Test empty example blocks
    - Verify graceful degradation (help still renders, possibly with warnings)

2. **Help coverage tests**
    - Test for commands missing help entirely (should error or warn)
    - Test for orphaned help files (referenced but not routed)
    - Test for parameters with missing descriptions
    - Test parameter set naming and aliases

3. **Surface-specific example filtering**
    - Test CLI path filters PowerShell examples correctly
    - Test PowerShell path gets all examples (CLI + PowerShell)
    - Test mixed language examples in same file
    - Test subcommand help loads correct file

4. **Performance and caching**
    - Test `MarkdownCommandHelpProvider` caches parsed content
    - Test repeated help lookups don't re-read files
    - Test cold-start latency is acceptable

5. **Help tool validation logic tests**
    - Location: Add new test class `HelpToolValidationTests.cs` to `tests/Cli.Tests/`
    - Test markdown structure validation:
        - Test fails when `Summary:` field is missing
        - Test fails when `Description:` field is missing
        - Test fails when no examples are present
        - Test fails when code fences are unbalanced
    - Test help completeness validation:
        - Test fails when routed CLI command has no corresponding help markdown file (error case)
        - Test warns (doesn't fail) when orphaned help markdown files exist (warning case)
        - Test passes when all routed commands have help files
        - Test passes when only index.md exists for root command
    - Test command key normalization:
        - Test multi-word command keys (e.g., "config get") map to correct filenames ("config-get.md")
        - Test extension name case-insensitivity

**Exit criteria:**

- Tests cover normal and error paths for help loading
- Malformed markdown handling is well-specified and tested
- Surface-specific filtering verified end-to-end
- Performance baselines established
- Help tool validation (15.3.2) has comprehensive unit test coverage

---

### 15.5 ⏳ Phase 5: Final Consistency Pass and Polish

**Objective:** Document final design, update all specs, and ensure consistency across codebase.

**Subtasks:**

1. **Migrate help markdown to YAML frontmatter structure**
    - Convert all help files to use YAML frontmatter for metadata (summary, description, tags, status)
    - Reorganize example sections with clear CLI/PowerShell subdivisions
    - Update `MarkdownCommandHelpProvider` to parse YAML frontmatter + structure
    - Update validation in help tool to validate YAML structure
    - Verify all help files parse correctly post-migration
    - Benefits: explicit structure, easier parsing, enables future enhancements (tags, status markers, relatedLinks)

2. **Spec updates**
    - Finalize [docs/spec/help-system.md](./help-system.md) with actual implementation and YAML frontmatter format
    - Update [docs/spec/ARCHITECTURE.md](./ARCHITECTURE.md) to reference help design
    - Review [docs/howto/help.md](../howto/help.md) for accuracy and completeness

3. **Code documentation**
    - Add XML comments to `MarkdownCommandHelpProvider`, `ExtensionIndexHelpAdapter`, and `ExtensionHelpManifestBuilder`
    - Document YAML frontmatter parsing logic
    - Document example language tag expectations
    - Document fallback behavior (markdown → attributes)

4. **Developer guides**
    - Ensure [docs/howto/help.md](../howto/help.md) covers all scenarios including YAML frontmatter structure
    - Add troubleshooting section for common help authoring issues
    - Provide copy-paste templates for new command help (with YAML frontmatter)

5. **MSLearn publishing pipeline (future)**
    - Design help export tool for markdown publication
    - Define output format for docs publishing
    - Integrate with CI/release workflow

**Exit criteria:**

- All specs accurately describe implemented behavior
- Code is well-commented and discoverable
- Developer workflows are documented and tested
- No contradictions between spec, code, and howto guides
