# Design Spec: Help System

## Status: Proposal

> This document describes the **planned** help system for Cirrus. It does not describe current behavior.
> See [ARCHITECTURE.md](ARCHITECTURE.md) for the current implementation state.

---

## 1. Goals

| Goal                                  | Rationale                                                                                                                  |
| ------------------------------------- | -------------------------------------------------------------------------------------------------------------------------- |
| **Single source of truth**            | Help content lives in one place per command/parameter; no duplication between CLI and PowerShell surfaces                  |
| **Tree-aware CLI help**               | `azv2 --help`, `azv2 vm --help`, `azv2 vm start --help` each produce meaningfully different output, mirroring azure-cli UX |
| **Flat PowerShell help**              | `Get-Help New-ResourceGroup -Full` follows standard MAML conventions; cmdlet discovery via `Get-Command`                   |
| **Fast at runtime**                   | CLI help resolves from lightweight extension-owned metadata without loading command execution code on the cold path        |
| **No markdown dependency at runtime** | CLI runtime help does not read markdown files; markdown is a build/publishing artifact only                                |
| **Authorable by humans**              | Descriptions and examples live close to the code; a developer can add help without knowing the rendering pipeline          |
| **AI-augmentable**                    | The agent can answer "how do I…" using the same help data that drives the terminal output                                  |

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

## 3. Single Source of Truth: The Attribute Model

All help content originates from C# attributes on processor classes. This is already mostly true today — the gap is completeness and examples.

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

Examples are authored directly in `ExtensionIndex.cs` as `CommandExample` entries on the `CommandMetadata` record. This keeps examples co-located with routing metadata and eliminates a separate file format.

```csharp
private static readonly CommandMetadata s_login_CLI = new(
    Name: "LoginCommand",
    ...
    Summary: "Authenticate to Azure and set the current account context.",
    Description: "Authenticate to Azure and persist account context for subsequent commands.",
    Examples:
    [
        new("Login with subscription id", "azv2 login --subscription-id 00000000-0000-0000-0000-000000000000"),
        new("Login with device code",     "azv2 login --subscription-id 00000000-0000-0000-0000-000000000000 --use-device-code"),
    ]
);
```

At build time, the `ExtensionHelpManifestBuilder` reads examples from `CommandMetadata.Examples` and writes them into `HelpManifest.json` for tooling/export workflows.

---

## 4. Build-Time and Tooling Extraction

At build time, the extension generation pipeline produces command metadata for routing/discovery. Runtime CLI help reads command metadata from Engine APIs backed by generated `ExtensionIndex.cs`. Optional tooling can consume the same canonical help inputs (`[ProcessorInfo]`, `[ArgumentInfo]`, and `CommandMetadata.Examples`) to emit documentation artifacts such as:

1. **A `HelpManifest.json`** per extension — for tooling, testing, and publishing workflows.
2. **A CLI help index** (`HelpIndex.json`) — for tooling/export scenarios such as markdown generation or static validation.

```json
{
    "extension": "Resources",
    "version": "1.0.0",
    "commands": [
        {
            "name": "create",
            "cliName": "group create",
            "psName": "New-ResourceGroup",
            "summary": "Create a resource group.",
            "description": "Create a new resource group in Azure.",
            "parameters": [
                {
                    "name": "name",
                    "psName": "Name",
                    "type": "string",
                    "required": true,
                    "description": "The name of the resource group.",
                    "aliases": [],
                    "allowedValues": [],
                    "parameterSetName": "(All)",
                    "supportsPipeline": false,
                    "supportsWildcards": false
                }
            ],
            "examples": {
                "cli": [
                    {
                        "title": "Create a resource group in East US",
                        "command": "azv2 group create --name myRg --location eastus"
                    }
                ],
                "powershell": [
                    {
                        "title": "Create a resource group in East US",
                        "command": "New-ResourceGroup -Name myRg -Location eastus"
                    }
                ]
            }
        }
    ]
}
```

**Why separate runtime from tooling?**  
Build-time artifacts are useful for publishing, validation, and snapshot testing, but CLI runtime help must remain on a fast cold path. Runtime help therefore comes from Engine metadata APIs (backed by `ExtensionIndex.cs`) and does not depend on markdown/manifests/index files.

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

### 5.6 Runtime metadata contract and ownership

CLI runtime help resolves through Engine APIs. Engine owns metadata retrieval; CLI owns tree resolution and rendering.

Runtime API shape:

```csharp
public interface IHelpMetadataService
{
    CommandMetadata GetCommandMetadata(string extensionName, string commandKey);
    IReadOnlyDictionary<string, CommandMetadata> GetCommandMetadataDictionary(string extensionName);
}
```

Engine API outputs are `CommandMetadata` or a dictionary of `CommandMetadata`, consistent with extension-generated `ExtensionIndex.cs`.

CLI then projects this metadata into runtime renderer DTOs:

- `RuntimeHelpGroup`
- `RuntimeHelpGroupSummary`
- `RuntimeHelpCommandSummary`
- `RuntimeHelpCommand`
- `RuntimeHelpParameter`
- `RuntimeHelpExample`

These DTOs are intentionally CLI-runtime-focused and lightweight. They are separate from markdown/manifests/indexes, which remain optional tooling artifacts.

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
        ▼ (source generator at build time)
        ├─► ExtensionIndex.cs (C# — for runtime dispatch, outside help-system ownership)
        ├─► Engine metadata APIs (single command or dictionary retrieval)
        ├─► CLI runtime help projection + renderer
        ├─► HelpManifest.json (optional tooling/publishing artifact)
        ├─► HelpIndex.json (optional tooling/export artifact)
        ├─► PS exports/*.ps1 (optional HelpMessage emission)
        └─► PS/<Module>/<Module>.dll-Help.xml (optional packaging/publishing artifact)
```

---

## 8. Performance Considerations

| Concern                                            | Mitigation                                                                                                   |
| -------------------------------------------------- | ------------------------------------------------------------------------------------------------------------ |
| Loading execution assemblies just to show `--help` | CLI help resolves from lightweight extension-owned metadata and must not load command execution assemblies   |
| Cold-start help latency                            | CLI intercepts `--help` before command execution and renders from lightweight help metadata only             |
| Reflection cost in runtime help path               | Runtime help uses Engine metadata APIs backed by pre-generated extension indexes                             |
| Tooling artifact size                              | Optional manifests/indexes are for tooling and publishing only; runtime does not depend on them              |
| Markdown/readme drift concerns                     | Markdown is generated from the same source but never used as runtime help input                              |
| PowerShell help startup latency                    | Prebuilt MAML XML is produced at build time and consumed by `Get-Help` without runtime command instantiation |

---

## 9. AI Agent Integration

The AI agent (natural language fallback) already answers "how do I…" questions. After this spec:

- The agent's system prompt is augmented with a compact command table derived from the same extension-owned help metadata used by CLI runtime help.
- The agent can reference the same surface-specific example collections used by CLI and PowerShell help to suggest real runnable commands rather than hallucinating syntax.
- A future `HelpTool` (Azure OpenAI function tool) lets the agent query help for a specific command/parameter during a conversation.

---

## 10. Examples Generation (AI-assisted)

For commands where no human-authored examples exist, a **build-time AI step** (opt-in, not blocking CI by default) can suggest examples using Azure OpenAI. Suggested examples are added to the `Examples` list in the relevant `CommandMetadata` field in `ExtensionIndex.cs`, reviewed by the developer, and committed to the repository.

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

1. The release pipeline runs `cirrus help export --format markdown --out ./docs/reference/` to generate per-command markdown files.
2. A PR is opened in the `azure-docs-cirrus` repo (equivalent to `azure-docs-cli` / `azure-docs-powershell`).
3. The markdown files are rendered on MSLearn.

The `cirrus help export` command is implemented as tooling or a tooling-facing command, consuming the same canonical help inputs and optional tooling artifacts. It is not used by runtime CLI help retrieval.

---

## 14. Decisions

The following design decisions are considered settled for the initial implementation of the Cirrus help system:

| Area                           | Decision                                                                                                                                                                                                                                            |
| ------------------------------ | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `Summary` authoring            | Keep `Summary` as a separate field with manual authoring.                                                                                                                                                                                           |
| Runtime CLI help source        | Runtime CLI help reads command metadata through Engine APIs backed by generated `ExtensionIndex.cs`; markdown/manifests/indexes are not runtime dependencies.                                                                                       |
| Runtime ownership split        | Engine owns metadata retrieval (single command or dictionary). CLI owns nested command-tree resolution and rendering.                                                                                                                               |
| CLI command key contract       | Use canonical lower-case, full-path, space-delimited command keys for deterministic group prefix and command exact lookup.                                                                                                                          |
| PowerShell help generation     | Keep PowerShell runtime help native to PowerShell. Generate MAML and related outputs only for packaging/publishing workflows. Keep PlatyPS optional for downstream publishing only.                                                                 |
| Examples authoring             | Examples are authored inline in `ExtensionIndex.cs` as `CommandExample` entries on `CommandMetadata`. `ExtensionIndex.cs` is the single source of truth; no per-surface sidecar files are used.                                                     |
| `HelpManifest.json` versioning | Do not introduce explicit manifest schema versioning yet. Rely on repo/build artifact compatibility for now, and add a lightweight version contract later only if independent extension shipping or host/extension skew becomes a real requirement. |

---

## 15. Implementation Plan

Implementation should proceed in small vertical phases so the canonical help model is validated first, then runtime CLI help can be wired on a cold-start-safe path.

### 15.1 Phase 1: Shared Help Model and Build Outputs

**Objective:** establish the canonical help model and generate the tooling/export artifacts that later phases may consume.

**In scope:**

1. Extend `ProcessorInfoAttribute` and `ArgumentInfoAttribute` with the agreed metadata surface (`Summary`, parameter-set metadata, pipeline/wildcard flags).
2. Define canonical help domain models for commands, parameters, examples, and surface projections.
3. Add `Summary`, `Description`, and `Examples` fields to `CommandMetadata`; populate them in each extension's `ExtensionIndex.cs`.
4. Consume canonical extension metadata plus help-specific inputs to generate optional `HelpManifest.json` per extension for tooling/testing.
5. Generate optional `HelpIndex.json` for tooling/export scenarios.
6. Add build validation that required descriptions, summaries, and per-surface examples are present.

**Out of scope:**

1. Wiring `azv2 --help` / group / command help rendering into the live CLI path.
2. Generating PowerShell MAML.
3. Updating generated PowerShell exports with `HelpMessage`.
4. AI help/tool integration.
5. MSLearn export.

**Primary deliverables:**

1. Shared help contracts in code (`CommandMetadata` with `Summary`, `Description`, `Examples`).
2. A generator or equivalent build step that produces optional `HelpManifest.json` and `HelpIndex.json` tooling artifacts from canonical extension/help metadata.
3. CI/build checks for missing required help content.

**Recommended first extension target:**

1. Start with one extension that has a small command surface and clear existing metadata, such as `Login` or `Resources`.
2. Use it to validate manifest shape, example loading, and generator ergonomics before rolling out broadly.

**Exit criteria:**

1. At least one extension builds with generated tooling help artifacts.
2. The generated tooling artifacts can be inspected and snapshot-tested.
3. The build fails when required help metadata is missing.
4. The generated outputs are stable enough to snapshot-test or golden-file test.

### 15.2 Phase 2: CLI Runtime Help

Intercept `--help` in CLI before execution, call Engine metadata APIs (single command or dictionary), and render root/group/command help from metadata without loading command execution code.

**Primary deliverables:**

1. `IHelpMetadataService` (or equivalent Engine API) implemented and exposed to CLI.
2. CLI help interception path (`-h` / `--help` / `help`) wired before command execution.
3. Canonical command-key contract implemented for exact and prefix lookups in nested command trees.
4. Runtime renderer wired to `RuntimeHelpGroup` / `RuntimeHelpCommand` DTOs.
5. Tests that verify the help path does not instantiate processors.

### 15.3 Phase 3: PowerShell Runtime Help

Keep runtime help native to PowerShell and add packaging/publishing-time generation for MAML and `HelpMessage` outputs where needed.

### 15.4 Phase 4: Validation, Agent, and Publishing

Add stronger CI checks, agent-side help consumption, and markdown/reference export for downstream documentation publishing.
