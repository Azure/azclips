# Design Spec: Help System

## Status: Proposal

> This document describes the **planned** help system for Cirrus. It does not describe current behavior.
> See [ARCHITECTURE.md](ARCHITECTURE.md) for the current implementation state.

---

## 1. Goals

| Goal                       | Rationale                                                                                                                  |
| -------------------------- | -------------------------------------------------------------------------------------------------------------------------- |
| **Single source of truth** | Help content lives in one place per command/parameter; no duplication between CLI and PowerShell surfaces                  |
| **Tree-aware CLI help**    | `azv2 --help`, `azv2 vm --help`, `azv2 vm start --help` each produce meaningfully different output, mirroring azure-cli UX |
| **Flat PowerShell help**   | `Get-Help New-ResourceGroup -Full` follows standard MAML conventions; cmdlet discovery via `Get-Command`                   |
| **Fast at runtime**        | Help data is extracted at build time and embedded; no reflection on the hot path                                           |
| **Authorable by humans**   | Descriptions and examples live close to the code; a developer can add help without knowing the rendering pipeline          |
| **AI-augmentable**         | The agent can answer "how do I…" using the same help data that drives the terminal output                                  |

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
    public string[] Examples { get; set; } = [];    // see §4
}
```

`Description` is the long-form text shown in `--help` at the command level.  
`Summary` is the short one-liner shown in parent group tables (like `az vm --help` listing commands).  
If `Summary` is empty, the first sentence of `Description` is used as a fallback.

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

Examples are surface-specific. Cirrus keeps a shared command identity and semantics, then stores runnable examples per UI surface.

**Decision:** use per-surface examples for CLI and PowerShell.  
Default authoring is in sidecar files keyed by canonical command identity:

- `<Processor>.examples.cli.json`
- `<Processor>.examples.ps.json`

Optional inline examples on `[ProcessorInfo]` may be used for a minimal seed example, but per-surface sidecars are authoritative when present.

Example shape in sidecars:

```json
{
    "commandId": "resources.group.create",
    "examples": [
        {
            "title": "Create a resource group in East US",
            "command": "azv2 group create --name myRg --location eastus"
        }
    ]
}
```

```json
{
    "commandId": "resources.group.create",
    "examples": [
        {
            "title": "Create a resource group in East US",
            "command": "New-ResourceGroup -Name myRg -Location eastus"
        }
    ]
}
```

---

## 4. Build-Time Extraction

At build time, a **Roslyn incremental source generator** (extending the planned `Core.Index.Generator` from the extension framework spec) walks all `[ProcessorInfo]` / `[ArgumentInfo]` attributes and emits:

1. **The `ExtensionIndex`** — already planned in the extension framework spec; gains `Description`, `Summary`, `Examples`, and the new `[ArgumentInfo]` fields in the generated `ParameterMetadata`.
2. **A `HelpManifest.json`** per extension — embedded as a resource into the extension DLL. Contains the full tree of command names → descriptions → parameters → surface-specific examples. Used by the CLI host's help system without loading processor types.
3. **A CLI help index** (`HelpIndex.json`) — generated at build time, lightweight map for root/group/command lookup without loading execution assemblies.

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

**Why build-time?**  
Loading assemblies and reflecting over attributes at runtime is slow and requires the extension to be loaded before the user can see help. With a pre-built index and manifest, `azv2 group --help` can render without loading command execution assemblies.

> **Fallback:** If no manifest is present (e.g., third-party extension built without the generator), the CLI host falls back to runtime reflection over `IExtensionIndex`.

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

Groups at root level come from loading just the `HelpManifest.json` from each extension. No processor code runs.

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

The existing `HelpCell` / `HelpLine` / `MultiGroupHelpTable` classes are sufficient for the table layout. The commented-out templates (`CommandHelpTemplate`, `CommandGroupHelpTemplate`) should be un-commented and completed. The rendering contract:

```
IHelpRenderer
  RenderRoot(IEnumerable<ExtensionManifest> extensions, uint? terminalWidth) → string[]
  RenderGroup(ExtensionManifest extension, string prefix, uint? terminalWidth) → string[]
  RenderCommand(CommandHelpData command, uint? terminalWidth) → string[]
```

`IHelpRenderer` is injected at startup and can be swapped for testing.

### 5.6 Global arguments

Global arguments (`--help`, `--debug`, `--output`) are defined once in the CLI host and injected into every command-level and group-level help page. Extensions do not re-declare them.

---

## 6. PowerShell Help Surface

### 6.1 Approach: External MAML + auto-generated `.psm1` `HelpMessage`

PowerShell has two standard help delivery mechanisms:

| Mechanism                               | Description                                                                     | When to use                                              |
| --------------------------------------- | ------------------------------------------------------------------------------- | -------------------------------------------------------- |
| **`HelpMessage` in `[Parameter()]`**    | Inline; shown with `-?` but not `Get-Help -Full`                                | Quick inline hint; always present                        |
| **External MAML XML** (`.dll-Help.xml`) | Full Get-Help support: description, syntax, parameters, examples, related links | Required for `Get-Help -Full`, `-Examples`, `-Parameter` |

Cirrus uses **both**:

- The PS script generator (already planned for the extension framework) emits `HelpMessage` on every parameter using `ParameterMetadata.Description` from the manifest.
- A separate **MAML generator** step produces `<ModuleName>.dll-Help.xml` files per PS module at build time, placed alongside the `.psd1` manifest.

The MAML generator reads the same `HelpManifest.json` embedded in each extension DLL, so there is no new authoring step.

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

Each PS module gets a top-level help file (`Az.Resources.md` equivalent) generated from the extension manifest. This is intended for MSLearn documentation publishing, not for runtime `Get-Help`.

---

## 7. Centralization: No Duplication

The current `ExtensionIndex` duplicates parameter descriptions in both the PS and CLI static tables. After this spec is implemented:

| Before                                                                                                        | After                                                            |
| ------------------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------- |
| Descriptions in `[ArgumentInfo]` **and** in PS `CommandMetadata` table **and** in CLI `CommandMetadata` table | Descriptions **only** in `[ArgumentInfo]` on the processor class |
| PS exports have no `HelpMessage`                                                                              | PS exports auto-generated with `HelpMessage` from manifest       |
| No MAML XML                                                                                                   | MAML generated from manifest at build time                       |
| `ExtensionIndex` written by hand                                                                              | `ExtensionIndex` generated by source generator                   |

The single path is:

```
[ProcessorInfo] / [ArgumentInfo] attributes
        │
        ▼ (source generator at build time)
        ├─► ExtensionIndex.cs (C# — for runtime dispatch)
        ├─► HelpManifest.json (embedded resource — for help without loading processor)
        ├─► PS exports/*.ps1 (with HelpMessage)
        └─► PS/<Module>/<Module>.dll-Help.xml (MAML)
```

---

## 8. Performance Considerations

| Concern                                            | Mitigation                                                                                                                             |
| -------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------- |
| Loading execution assemblies just to show `--help` | `HelpIndex.json` + `HelpManifest.json` are read from prebuilt metadata artifacts; help path does not load execution assemblies         |
| Slow reflection at runtime                         | All metadata extracted at build time by source generator                                                                               |
| Large manifest files                               | Each manifest covers one extension; root help uses index-first lookup and only reads needed manifests (streaming JSON deserialization) |
| First-call latency for command-level help          | Index/manifest are loaded on first `-h` and cached for the session; command execution dependencies remain unloaded                     |
| PowerShell help startup latency                    | Prebuilt MAML XML is produced at build time and consumed by `Get-Help` without runtime command instantiation                           |

---

## 9. AI Agent Integration

The AI agent (natural language fallback) already answers "how do I…" questions. After this spec:

- The agent's system prompt is augmented with a compact command table derived from all loaded `HelpManifest.json` files at startup.
- The agent can reference the same `Examples` array to suggest real runnable commands rather than hallucinating syntax.
- A future `HelpTool` (Azure OpenAI function tool) lets the agent query help for a specific command/parameter during a conversation.

---

## 10. Examples Generation (AI-assisted)

For commands where no human-authored examples exist, a **build-time AI step** (opt-in, not blocking CI by default) can suggest examples using Azure OpenAI. Suggested examples are written to `<Processor>.examples.cli.json` and/or `<Processor>.examples.ps.json` sidecar files, checked in by the developer, and incorporated into the manifest.

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

The `cirrus help export` command is implemented as a Cirrus extension command itself, consuming the same `IHelpRenderer` and `HelpManifest.json` data.

---

## 14. Open Questions

| Question                                                                             | Options                                                                               | Decision needed by            | Status                                                                                                                                                                                                                                                |
| ------------------------------------------------------------------------------------ | ------------------------------------------------------------------------------------- | ----------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Should `Summary` be a separate attribute field or auto-derived from `Description`?   | Separate field (more control) vs. auto (less authoring)                               | Before implementation         | Decided: keep separate field with manual authoring                                                                                                                                                                                                    |
| MAML vs. PlatyPS for PS help generation?                                             | Hand-rolled MAML generator (full control) vs. PlatyPS (community standard, MD → MAML) | Before PS help implementation | Decided: shared canonical help entities with surface-specific projection; custom MAML for PS runtime help, CLI manifest/renderer for CLI help, PlatyPS optional for downstream publishing                                                             |
| Should examples be inline in `[ProcessorInfo]` or in sidecar `.examples.json` files? | Inline is simpler; sidecar allows richer markdown and AI-generated content            | Before implementation         | Decided: examples are surface-specific; use per-surface sidecars for CLI and PowerShell with shared command identity; optional inline seed examples only                                                                                              |
| Who owns the `HelpManifest.json` schema versioning?                                  | Semver the schema; host rejects manifests with incompatible major version             | Before first extension ships  | Decided: do not introduce explicit manifest schema versioning yet; rely on repo/build artifact compatibility for now, and add a lightweight version contract only if independent extension shipping or host/extension skew becomes a real requirement |
