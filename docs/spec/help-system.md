# Design Spec: Help System

## Status: Proposal

> This document describes the **planned** help system for Cirrus. It does not describe current behavior.
> See [ARCHITECTURE.md](ARCHITECTURE.md) for the current implementation state.
> Current tooling snapshot: `tools/Help/Cirrus.Help.Tool` emits `HelpManifest.json`, `HelpIndex.json`, and generated PowerShell XML help files under `artifacts/help/powershell/`.

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

## 2. Current State (gaps to close)

| Area                   | Current state                                                                             | Gap                                                   |
| ---------------------- | ----------------------------------------------------------------------------------------- | ----------------------------------------------------- |
| Parameter descriptions | `[ArgumentInfo(Description = "...")]` on processor properties                             | ✅ Already exists — descriptions are in code          |
| Command descriptions   | `[ProcessorInfo(Description = "...")]` on processor classes                               | Partially used; not always populated                  |
| CLI help rendering     | `HelpCell`, `HelpLine`, `MultiGroupHelpTable` exist                                       | Templates are all commented out; not wired to routing |
| CLI group/tree help    | Commented-out `CommandGroupHelpTemplate`                                                  | Not implemented                                       |
| PS parameter help      | `[Parameter(Mandatory = ...)]` only                                                       | No `HelpMessage`, no external MAML XML                |
| Examples               | None                                                                                      | No examples anywhere                                  |
| Metadata duplication   | `ExtensionIndex` copies descriptions into both PS and CLI `CommandMetadata` static tables | Descriptions written twice; divergence risk           |
| CI enforcement         | No linting                                                                                | No check that required help fields are populated      |

---

## 3. Single Source of Truth: Extension Metadata

All help content currently originates from extension metadata (`CommandMetadata`, `ParameterMetadata`, `ProcessorInfo`, and `ArgumentInfo`). Build-time tooling projects this metadata into optional external artifacts.

### 3.1 `[ProcessorInfo]` (class-level)

```csharp
[AttributeUsage(AttributeTargets.Class)]
public class ProcessorInfoAttribute : Attribute
{
    public string Name { get; set; } = "";          // existing
    public string Description { get; set; } = "";   // existing

    // NEW
    public string Summary { get; set; } = "";       // one-line; used in group-level help tables
    public string[] Examples { get; set; } = [];    // optional seed examples; see §3.3
}
```

`Description` is the long-form text shown in `--help` at the command level.  
`Summary` is the short one-liner shown in parent group tables (like `az vm --help` listing commands).  
`Summary` is authored explicitly; it is not auto-derived from `Description`.

### 3.2 `[ArgumentInfo]` (property-level)

```csharp
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public class ArgumentInfoAttribute : Attribute
{
    public bool Required { get; set; } = false;      // existing
    public string Description { get; set; } = "";    // existing
    public string[] Enums { get; set; } = [];        // existing
    public string[] Aliases { get; set; } = [];      // existing
    public ArgumentAction Action { get; set; } = ArgumentAction.Store; // existing
    public string SerializedName { get; set; } = ""; // existing

    // NEW
    public string Summary { get; set; } = "";        // short form for compact tables
    public string ParameterSetName { get; set; } = "(All)"; // for PS-style help output
    public bool SupportsPipeline { get; set; } = false;
    public bool SupportsWildcards { get; set; } = false;
}
```

### 3.3 Examples

Examples are authored in extension metadata as `CommandExample` entries attached to `CommandMetadata`. This keeps examples close to command routing metadata and avoids runtime file dependencies.

At build time, the help generator reads extension metadata and emits external artifacts. At runtime, CLI help renders examples directly from metadata.

---

## 4. Build-Time and Tooling Extraction

At build time, the extension generation pipeline produces command metadata for routing/discovery. Runtime CLI help reads command metadata from Engine APIs backed by generated extension indexes and renders syntax, parameters, descriptions, and examples directly from that metadata. Optional build-time tooling can consume the same metadata to emit external artifacts when there is a concrete validation, packaging, or publishing need.

### 4.1 Ownership boundary

Build-time help generation is excluded from Cirrus runtime ownership.

- `src/` owns runtime code and metadata: `Core`, `Extensions`, `CLI`, `PS`.
- `tools/` owns build-time help generation, validation, markdown export, and packaging-oriented help generation.
- Runtime clients do not read generated help artifacts.

The intended layout is:

```text
/src
    /Core
    /Extensions
    /CLI
    /PS
/tools
    /Help
        help-export
        help-validate
        help-ps
```

### 4.2 Artifact locations

Generated help artifacts should be treated as optional build outputs, not source inputs.

- Staging output: `artifacts/help/`
- Per-extension machine-readable output: `artifacts/help/<ExtensionName>/...`
- Cross-extension shared outputs: `artifacts/help/...`
- Generated PowerShell help XML: `artifacts/help/powershell/<ModuleName>.dll-Help.xml`
- Optional published markdown output: `artifacts/help/markdown/` or a caller-provided output path such as `docs/reference/`

These artifacts may be copied, packaged, snapshot-tested, or published by tooling, but they are not read by runtime CLI help retrieval and are not used directly by `Get-Help` unless packaging copies the generated MAML into the shipped PowerShell module layout.

### 4.3 Interaction model

The interaction model is:

1. A build-time tool under `tools/` loads extension metadata from built assemblies or project outputs.
2. The tool reads extension metadata through `IExtensionIndex` and related contracts.
3. The tool emits artifacts into `artifacts/help/`.
4. Downstream steps may validate, snapshot-test, package, or publish those artifacts.
5. Runtime clients continue to resolve help from metadata or native session state, not from generated files.
6. When present, CLI examples come from the same runtime metadata and are rendered in `azv2 ... --help` output.

Examples of concrete outputs include generated JSON manifests/indexes and PowerShell XML help files. Those outputs are downstream projections of the same metadata; they do not replace runtime CLI examples. Machine-readable intermediate formats are optional implementation details of tooling, not part of the runtime contract.

**Why separate runtime from tooling?**  
Build-time artifacts are useful for publishing, validation, and snapshot testing, but CLI runtime help must remain on a fast cold path. Runtime help therefore comes from Engine metadata APIs backed by extension metadata and does not depend on markdown/manifests/index files. This keeps the runtime mental model simple: Cirrus serves runtime metadata and rendering; external tooling generates files.

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

### 5.2 Root-level help (`azv2 --help`)

```
azv2 - Azure management CLI with AI fallback

Usage:
    azv2 [group] [command] [arguments...] [--help]
    azv2 "<natural language request>"

Groups:
    group     : Manage resource groups.
    login     : Authenticate to Azure.
    vm        : Manage virtual machines.
    extension : Manage Cirrus extensions.

Global Arguments:
    --help -h : Show this help message and exit.
    --debug   : Increase logging verbosity.
    --output  : Output format. Allowed: json, table, none. Default: table.

Learn more: https://aka.ms/cirrus
```

Root-level and group-level CLI help resolve from Engine metadata APIs (backed by extension command metadata). No command execution code runs.

### 5.3 Group-level help (`azv2 group --help`)

```
Group
    azv2 group : Manage resource groups.

Subgroups:
    (none)

Commands:
    create [Preview] : Create a resource group.
    delete           : Delete a resource group.
    list             : List resource groups in a subscription.
    show             : Show a resource group.

Global Arguments:
    --help -h : Show this help message and exit.
    --debug   : Increase logging verbosity.
```

Tags (`[Preview]`, `[Deprecated]`, `[Experimental]`) come from a `Tags` field added to `[ProcessorInfo]`.

### 5.4 Command-level help (`azv2 group create --help`)

```
Command
    azv2 group create : Create a resource group.

Arguments
    --name -n          [Required] : The name of the resource group.
    --location -l      [Required] : Location for resource group. Example: eastus, westus2.
    --subscription-id            : Azure Subscription ID. Defaults to the active subscription.

Global Arguments
    --help -h : Show this help message and exit.
    --debug   : Increase logging verbosity.

Examples
    Create a resource group in East US.
        azv2 group create --name myRg --location eastus

    Create a resource group using an explicit subscription.
        azv2 group create --name myRg --location eastus --subscription-id 00000000-0000-0000-0000-000000000000
```

### 5.5 Rendering pipeline

The existing `HelpCell` / `HelpLine` / `MultiGroupHelpTable` classes are sufficient for the table layout. The commented-out templates (`CommandHelpTemplate`, `CommandGroupHelpTemplate`) should be un-commented and completed. The renderer should consume extension help metadata resolved at runtime via the extension help contract:

```
IHelpRenderer
    RenderRoot(IEnumerable<RuntimeHelpGroup> groups, uint? terminalWidth) → string[]
    RenderGroup(RuntimeHelpGroup group, uint? terminalWidth) → string[]
    RenderCommand(RuntimeHelpCommand command, uint? terminalWidth) → string[]
```

`IHelpRenderer` is injected at startup and can be swapped for testing.

The projection from extension metadata into `RuntimeHelpGroup` / `RuntimeHelpCommand` is CLI-specific runtime logic. CLI runtime code shapes that metadata into terminal-friendly help output.

### 5.6 Runtime metadata contract and ownership

CLI runtime help resolves through Engine APIs. Engine owns metadata retrieval; CLI owns tree resolution, CLI-specific DTO projection, and rendering.

Runtime API shape:

```csharp
public interface IHelpMetadataService
{
    CommandMetadata GetCommandMetadata(string extensionName, string commandKey);
    IReadOnlyDictionary<string, CommandMetadata> GetCommandMetadataDictionary(string extensionName);
}
```

Engine API outputs are `CommandMetadata` or a keyed dictionary of `CommandMetadata`, consistent with generated extension indexes.

CLI then projects this metadata into runtime renderer DTOs:

- `RuntimeHelpGroup`
- `RuntimeHelpGroupSummary`
- `RuntimeHelpCommandSummary`
- `RuntimeHelpCommand`
- `RuntimeHelpParameter`
- `RuntimeHelpExample`

These DTOs are intentionally CLI-runtime-focused and lightweight. They are separate from markdown/manifests/indexes, which remain optional tooling artifacts.

Extensions should not implement this CLI projection by default. A generic CLI-side adapter can build these DTOs from keyed `CommandMetadata`, while PowerShell continues to use its own native runtime help path.

### 5.7 Canonical command key for nested CLI tree

To support nested CLI help efficiently, each CLI command uses a canonical key in metadata:

- lower-case
- space-delimited segments
- full command path from group root to command leaf

Examples:

- `group create`
- `vm create`
- `network nsg create`

Lookup rules:

- root help: CLI uses known top-level groups
- group help: CLI requests extension dictionary and filters by key prefix
- command help: CLI requests exact key

### 5.8 Runtime lifecycle and cold-path constraints

The CLI host follows this lifecycle for help:

1. Parse arguments and detect `-h` / `--help` / `help` before command execution.
2. Resolve target extension/group.
3. Call Engine metadata APIs for exact-key or dictionary retrieval.
4. Project metadata into CLI runtime help DTOs.
5. Render output via `IHelpRenderer`.
6. Exit help path without creating command processor instances.

Cold-path constraints for Phase 2:

- no runtime reflection scan over processor types in the help path
- no processor instantiation to render help
- help metadata may be cached for the process lifetime after first access
- no markdown file read in runtime help path

### 5.9 Global arguments

Global arguments (`--help`, `--debug`, `--output`) are defined once in the CLI host and injected into every command-level and group-level help page. Extensions do not re-declare them.

---

## 6. PowerShell Help Surface

### 6.1 Approach: Native PowerShell Help at Runtime

PowerShell has two standard help delivery mechanisms:

| Mechanism                               | Description                                                                     | When to use                                              |
| --------------------------------------- | ------------------------------------------------------------------------------- | -------------------------------------------------------- |
| **`HelpMessage` in `[Parameter()]`**    | Inline; shown with `-?` but not `Get-Help -Full`                                | Quick inline hint; always present                        |
| **External MAML XML** (`.dll-Help.xml`) | Full Get-Help support: description, syntax, parameters, examples, related links | Required for `Get-Help -Full`, `-Examples`, `-Parameter` |

Cirrus runtime does not implement a custom PowerShell help resolver. PowerShell continues to own runtime help resolution via `Get-Help`, using the cmdlet/session metadata and any generated external help that the PowerShell module ships.

PowerShell does not use the CLI runtime DTOs (`RuntimeHelpGroup`, `RuntimeHelpCommand`, and related types) or the CLI adapter that builds them. Both clients read from the same extension-owned metadata, but each client owns its own projection and runtime behavior.

Build-time tooling may still:

- emit `HelpMessage` on generated parameters where appropriate
- generate `<ModuleName>.dll-Help.xml` for PowerShell packaging and publishing workflows

Those outputs are build/package concerns, not part of Cirrus CLI runtime help resolution.

### 6.2 Example generated PS export (with HelpMessage)

```powershell
function New-ResourceGroup {
    [CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'Medium')]
    param (
        [Parameter(Mandatory = $false, HelpMessage = "Azure Subscription ID. Defaults to the active subscription.")]
        [string] $SubscriptionId,

        [Parameter(Mandatory = $true, HelpMessage = "The name of the resource group.")]
        [string] $Name,

        [Parameter(Mandatory = $true, HelpMessage = "Location for resource group.")]
        [string] $Location
    )
    ...
}
```

### 6.3 `Get-Help` output (from MAML)

```
NAME
    New-ResourceGroup

SYNTAX
    New-ResourceGroup [-Name] <string> [-Location] <string> [[-SubscriptionId] <string>]

DESCRIPTION
    Create a new resource group in Azure.

PARAMETERS
    -Name <string>
        The name of the resource group.

        Required?                    true
        Position?                    Named
        Accept pipeline input?       false
        Parameter set name           (All)
        Aliases                      None

    -SubscriptionId <string>
        Azure Subscription ID. Defaults to the active subscription.

        Required?                    false
        Position?                    Named
        Accept pipeline input?       false
        Parameter set name           (All)
        Aliases                      None

EXAMPLES
    Example 1: Create a resource group in East US
        New-ResourceGroup -Name myRg -Location eastus

RELATED LINKS
    https://aka.ms/cirrus/New-ResourceGroup
```

### 6.4 Module-level help file

Each PS module may also get a top-level help file (`Az.Resources.md` equivalent) for publishing workflows. This is intended for MSLearn documentation publishing, not for runtime `Get-Help`.

---

## 7. Centralization: No Duplication

The current `ExtensionIndex` duplicates parameter descriptions in both the PS and CLI static tables. After this spec is implemented:

| Before                                                                                                        | After                                                                                                                                                         |
| ------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Descriptions in `[ArgumentInfo]` **and** in PS `CommandMetadata` table **and** in CLI `CommandMetadata` table | Descriptions **only** in `[ArgumentInfo]` on the processor class                                                                                              |
| PS exports have no `HelpMessage`                                                                              | PS packaging/tooling can emit `HelpMessage` and external help without changing runtime ownership                                                              |
| No MAML XML                                                                                                   | MAML can be generated as a packaging/tooling artifact                                                                                                         |
| `ExtensionIndex` written by hand                                                                              | `ExtensionIndex` remains an extension/codegen concern; runtime help is rendered from extension-owned metadata, and tooling artifacts are optional projections |

The single path is:

```
[ProcessorInfo] / [ArgumentInfo] attributes
        │
        ▼ (codegen/build at compile time)
        ├─► ExtensionIndex.cs (C# runtime dispatch/help metadata projection)
        ├─► Engine metadata APIs (single command or dictionary retrieval)
        ├─► CLI runtime help projection + renderer
        └─► /tools/Help/*
            ├─► artifacts/help/<Extension>/...
            ├─► artifacts/help/...
            ├─► artifacts/help/powershell/<Module>.dll-Help.xml
            ├─► artifacts/help/markdown/*
            └─► PS exports/*.ps1 (optional HelpMessage emission)
```

---

## 8. Performance Considerations

| Concern                                            | Mitigation                                                                                                   |
| -------------------------------------------------- | ------------------------------------------------------------------------------------------------------------ |
| Loading execution assemblies just to show `--help` | CLI help resolves from lightweight extension-owned metadata and must not load command execution assemblies   |
| Cold-start help latency                            | CLI intercepts `--help` before command execution and renders from lightweight help metadata only             |
| Reflection cost in runtime help path               | Runtime help uses Engine metadata APIs backed by pre-generated extension indexes                             |
| Tooling artifact size                              | Optional manifests/indexes are for tooling and publishing only; runtime does not depend on them              |
| Markdown/readme drift concerns                     | Optional markdown exports may be generated from metadata but are not runtime help inputs                     |
| PowerShell help startup latency                    | Prebuilt MAML XML is produced at build time and consumed by `Get-Help` without runtime command instantiation |

---

## 9. AI Agent Integration

The AI agent (natural language fallback) already answers "how do I…" questions. After this spec:

- The agent's system prompt is augmented with a compact command table derived from the same extension-owned help metadata used by CLI runtime help.
- The agent can reference the same surface-specific example collections used by CLI and PowerShell help to suggest real runnable commands rather than hallucinating syntax.
- A future `HelpTool` (Azure OpenAI function tool) lets the agent query help for a specific command/parameter during a conversation.

---

## 10. Examples Generation (AI-assisted)

For commands where no human-authored examples exist, a **build-time AI step** (opt-in, not blocking CI by default) can suggest examples using Azure OpenAI. Suggested examples are added to `CommandMetadata.Examples`, reviewed by the developer, and committed to the repository.

This keeps examples human-reviewed while reducing authoring burden.

---

## 11. Tags / Status Markers

`[ProcessorInfo]` gains a `Tags` field to drive status markers in help output:

```csharp
public enum HelpTag { Preview, Deprecated, Experimental, GA }

// in ProcessorInfoAttribute:
public HelpTag[] Tags { get; set; } = [];
```

Tag rendering:

- `[Preview]` → shown in group tables, no warning on use
- `[Deprecated]` → shown in group tables, warning printed on command invocation
- `[Experimental]` → shown in group tables, warning printed on command invocation

---

## 12. CI Enforcement

A build-time analyzer (Roslyn analyzer or a custom `dotnet tool`) checks:

| Rule                                                                   | Severity |
| ---------------------------------------------------------------------- | -------- |
| `[ProcessorInfo]` missing `Description`                                | Warning  |
| `[ProcessorInfo]` missing `Summary`                                    | Warning  |
| `[ArgumentInfo]` on a `Required = true` property missing `Description` | Error    |
| `[ArgumentInfo]` on any property missing `Description`                 | Warning  |
| Missing CLI examples for a command                                     | Warning  |
| Missing PowerShell examples for a command                              | Warning  |
| MAML XML out of sync with manifest (hash mismatch)                     | Error    |

The MAML sync check runs on every PR via a `dotnet tool` invocation in the CI pipeline. A PR that removes or changes a processor without regenerating the MAML will fail.

---

## 13. MSLearn Publishing (future)

When Cirrus reaches a release cadence:

1. The release pipeline runs a help export tool from `tools/Help/` to generate per-command markdown files, for example into `artifacts/help/markdown/` and then into `docs/reference/` as a publishing step.
2. A PR is opened in the `azure-docs-cirrus` repo (equivalent to `azure-docs-cli` / `azure-docs-powershell`).
3. The markdown files are rendered on MSLearn.

The export step is a build-time tooling concern, consuming the same canonical help inputs and optional tooling artifacts. It is not used by runtime CLI help retrieval.

---

## 14. Decisions

The following design decisions are considered settled for the initial implementation of the Cirrus help system:

| Area                       | Decision                                                                                                                                                                                                                 |
| -------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| `Summary` authoring        | Keep `Summary` as a separate field with manual authoring.                                                                                                                                                                |
| Runtime CLI help source    | Runtime CLI help reads command metadata through Engine APIs backed by a generated projection compiled from authored markdown help files; markdown/manifests/indexes are not runtime dependencies.                        |
| Runtime ownership split    | Engine owns metadata retrieval (single command or keyed dictionary). CLI owns nested command-tree resolution, CLI-specific DTO projection, and rendering. Extensions do not implement CLI projection by default.         |
| CLI command key contract   | Use canonical lower-case, full-path, space-delimited command keys for deterministic group prefix and command exact lookup.                                                                                               |
| PowerShell help generation | Keep PowerShell runtime help native to PowerShell. Generate MAML and related outputs only for packaging/publishing workflows. Keep PlatyPS optional for downstream publishing only.                                      |
| Tooling ownership          | Build-time help generation, markdown export, validation, and packaging-oriented projections live under root `tools/` and emit optional artifacts under `artifacts/help/`. They are not part of Cirrus runtime retrieval. |
| Examples authoring         | Examples are authored in the per-cmdlet markdown help files under each module's `help/` folder. The markdown is the single source of truth; no per-surface sidecar files are used.                                       |
| JSON tooling artifacts     | Defer `HelpManifest.json` / `HelpIndex.json` until there is a concrete tooling consumer. Runtime help does not require JSON artifacts.                                                                                   |

---

## 15. Implementation Plan

Implementation should proceed in small vertical phases so the canonical help model is validated first, then runtime CLI help can be wired on a cold-start-safe path.

### 15.1 Phase 1: Shared Help Model

**Objective:** establish the canonical help model used by runtime clients and available to future tooling.

**In scope:**

1. Extend `ProcessorInfoAttribute` and `ArgumentInfoAttribute` with the agreed metadata surface (`Summary`, parameter-set metadata, pipeline/wildcard flags).
2. Define canonical help domain models for commands, parameters, examples, and surface projections.
3. Add build tooling that converts authored markdown help into a runtime projection and PowerShell help outputs.
4. Ensure the projection model is sufficient for CLI runtime help and future PowerShell packaging/help generation.
5. Add build validation that required descriptions, summaries, and examples are present in the authored markdown.

**Out of scope:**

1. Wiring `azv2 --help` / group / command help rendering into the live CLI path.
2. Generating PowerShell MAML.
3. Updating generated PowerShell exports with `HelpMessage`.
4. AI help/tool integration.
5. MSLearn export.

**Primary deliverables:**

1. Shared help contracts in code for the runtime projection (`CommandMetadata` with `Summary`, `Description`, `Examples`).
2. Authored markdown help populated for at least one end-to-end module.
3. CI/build checks for missing required help content.

**Recommended first extension target:**

1. Start with one module that has a small command surface and clear existing markdown help, such as `Login` or `Resources`.
2. Use it to validate runtime help shape, example loading, and markdown authoring ergonomics before rolling out broadly.

**Exit criteria:**

1. At least one module exposes enough authored markdown help for CLI runtime help to render from the generated runtime projection alone.
2. The runtime help output can be snapshot-tested.
3. The build fails when required help metadata is missing.
4. The metadata is sufficient to support future packaging/publishing tooling without changing the runtime contract.

### 15.2 Phase 2: CLI Runtime Help

Intercept `--help` in CLI before execution, call Engine metadata APIs (single command or dictionary), and render root/group/command help from metadata without loading command execution code.

**Primary deliverables:**

1. `IHelpMetadataService` (or equivalent Engine API) implemented and exposed to CLI.
2. CLI help interception path (`-h` / `--help` / `help`) wired before command execution.
3. Canonical command-key contract implemented for exact and prefix lookups in nested command trees, including keyed metadata retrieval from extension indexes.
4. CLI-side runtime adapter wired to project keyed `CommandMetadata` into `RuntimeHelpGroup` / `RuntimeHelpCommand` DTOs.
5. Tests that verify the help path does not instantiate processors.

### 15.3 Phase 3: PowerShell Runtime Help

Keep runtime help native to PowerShell and add packaging/publishing-time generation for MAML and `HelpMessage` outputs where needed.

### 15.4 Phase 4: Validation, Agent, and Publishing

Add stronger CI checks, agent-side help consumption, and markdown/reference export for downstream documentation publishing.
