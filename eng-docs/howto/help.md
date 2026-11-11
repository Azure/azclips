# Adding Help Content

This guide explains how to author and test help for CLI commands and PowerShell cmdlets in Azclips.

CLI help and PowerShell help are authored separately, in different folders, with different formats:

| Surface    | Source                                             | Format                                                                    | Tool                                 |
| ---------- | -------------------------------------------------- | ------------------------------------------------------------------------- | ------------------------------------ |
| CLI        | `src/CLI/Help/Docs/<verb>/…/<command>.md` (nested) | Azclips markdown (frontmatter + bash fences)                              | `Azclips.Help.Tool` (validators)     |
| PowerShell | `src/PS/<Module>/help/<Cmdlet>.md`                 | PlatyPS schema 2024-05-01 (current `Microsoft.PowerShell.PlatyPS` schema) | `Microsoft.PowerShell.PlatyPS` 1.0.1 |

The CLI folder name is the **verb** — the first user-facing CLI token (`vm`, `disk`, `group`, `login`, `config`, `extension`, `deployment`). Verbs are declared by `[ExtensionVerb("...")]` attributes on the extension's `IExtensionIndex` class; one extension may contribute several verbs (e.g. `Compute` owns both `vm` and `disk`). The scaffolder and validators walk `IExtensionIndex.GetVerbs()` so the layout stays in sync with code automatically.

Both surfaces are validated and built by:

```powershell
.\build\build.ps1 -GenerateHelpArtifacts
```

## Discovery-First Help Model

Help resolution in Azclips is **discovery-first**:

- **Authoring time** (you): Scaffolder walks `IExtensionIndex.GetVerbs()` to find what verbs an extension contributes, generates markdown stubs under `src/CLI/Help/Docs/<verb>/`, you hand-fill them.
- **Runtime** (user): Azclips discovers what's actually installed on the user's machine, resolves verb ownership from discovered extensions (not static hints), and routes help requests accordingly.

This means:

- Help is always in sync with what's installed — no stale verb→package mappings.
- If a new extension is added to `~/.azclips/extensions/`, its verbs appear in help immediately.
- Unknown verbs suggest an install command via the embedded hint table (fallback only).

## Quick Start — CLI

1. **Scaffold templates** for a verb's CLI commands. Replace `<verb>` with the user-facing CLI noun you're authoring help for (e.g. `vm`, `disk`, `group`, `login`, `config`, `extension`):

    ```powershell
    dotnet run --project tools/Help/Azclips.Help.Tool -- --scaffold <verb>
    ```

    Example:

    ```powershell
    dotnet run --project tools/Help/Azclips.Help.Tool -- --scaffold vm
    ```

    This walks `IExtensionIndex.GetCommandTable(ClientType.CLI)` for the extension that contributes the verb, strips the verb prefix from each routed key, and emits one `.md` stub per command into `src/CLI/Help/Docs/<verb>/`. Existing files are left untouched (idempotent).

2. **Fill in `summary`, `Description`, and at least one bash example.** The scaffolder emits `TODO` markers in `summary` and `Description`, and an example pre-inlined with `--<name> <type>` shape placeholders. Both shapes fail CI on purpose — replace them with prose and a real, runnable invocation before merging. See [Placeholder rules](#placeholder-rules) below for what the validators accept inside example fences.

3. **Validate:**

    ```powershell
    .\build\build.ps1 -GenerateHelpArtifacts
    dotnet .\artifacts\Debug\Core.Cli\az.dll <group> <command> --help
    ```

## Quick Start — PowerShell

Build the cmdlet binaries and ensure the pinned PlatyPS dependency is available before
scaffolding or refreshing help:

```powershell
.\build\build.ps1
.\build\Install-BuildDependencies.ps1
```

1. **Scaffold per-cmdlet markdown** with PlatyPS:

    ```powershell
    .\build\New-PSHelpMarkdown.ps1                  # all modules under src/PS/
    .\build\New-PSHelpMarkdown.ps1 -Module Compute  # one module
    ```

    The script discovers modules from `src/PS/<Name>/<Name>.psd1` at runtime — add a new module folder with a manifest and the next run picks it up automatically (no script edits). Tab-completion on `-Module` enumerates the same set; an unknown name fails fast with the available list. The script imports the module's compiled cmdlet DLL in a child pwsh process and runs `New-MarkdownCommandHelp` against it, writing to `src/PS/<Module>/help/`. Existing files are not overwritten.

    **For changed cmdlets with existing help, use `-Refresh` instead:**

    ```powershell
    .\build\New-PSHelpMarkdown.ps1 -Module Accounts -Refresh
    ```

    This runs `Update-MarkdownCommandHelp` against every existing help file in the
    selected module. It synchronizes syntax and parameter metadata with the compiled
    cmdlets; it doesn't infer descriptions for new parameters or write new examples.
    Fill in any new `{{ Fill ... }}` placeholders and review the diff for unrelated
    date, formatting, or PlatyPS-generated changes before committing.

    For generic `-WhatIf` help, retain PowerShell's generated wording:
    "Runs the command in a mode that only reports what would happen without performing the actions."
    PlatyPS 1.0.1 appends the runtime description whenever it differs from the
    authored text, so alternative generic wording accumulates duplicates on refresh.
    Preserve command-specific WhatIf guidance where needed and review its refresh diff.

    Keep PlatyPS's internal `__AllParameterSets` headings in source help. Only the
    [Learn docs sync](ps-docs-sync.md) relabels those headings as `Default (Default)` in published copies;
    local help generation and metadata validation use the original source.

2. **Hand-fill** SYNOPSIS, DESCRIPTION, and EXAMPLES. Our post-processor (`Convert-PlatyPSPlaceholders` in [build/New-PSHelpMarkdown.ps1](../../build/New-PSHelpMarkdown.ps1)) replaces PlatyPS's `{{ Fill in the Synopsis }}` placeholders with `TODO -` markers and seeds at least one fenced example using the cmdlet's mandatory parameters shaped as `-Param <typename>`. Both shapes fail CI on purpose — search for `TODO` and replace each marker plus every `<typename>` placeholder in the example fence with real content. See [Placeholder rules](#placeholder-rules) below for the documented escape hatches.

    For optional sections such as NOTES, remove the `{{ Fill ... }}` placeholder if
    no content applies; retain the heading and leave its body empty. Learn omits
    empty Notes sections. Do not substitute boilerplate such as "Generated help for ...".

    Use RELATED LINKS as a curated "see also" list: point readers to useful companion
    commands or supporting guidance, such as getting a resource before updating it
    or preparing inputs for a creation command. It need not list every sibling
    command, and links do not have to be reciprocal.

    RELATED LINKS is optional. Leave it empty if no links apply. If you include
    a Markdown link, provide a destination, for example
    `[Get-AzVM](./Get-AzVM.md)`, or remove the entry. Empty destinations such as
    `[Online Version]()` fail validation; URLs are not checked over the network.

3. **Validate + generate MAML:**

    ```powershell
    .\build\Test-PSHelpMarkdown.ps1 -Module Compute
    .\build\build.ps1 -GenerateHelpArtifacts
    Import-Module .\artifacts\Debug\PS\Compute\Compute.psd1 -Force
    Get-Help New-AzVM -Full
    ```

    Help generation also normalizes PlatyPS example code fences and blank-line
    placeholders. Descriptions remain before their commands with a blank line
    between the description and code, using real newlines
    instead of U+0080 control characters that pagers can display as `?` or `<80>`.
    After regenerating help, use a fresh PowerShell session to avoid cached help.

## File Structure

```
src/CLI/Help/Docs/             # CLI markdown — Azclips format, one folder per verb
├── vm/                        # az vm …  (Compute extension)
│   ├── index.md               # az vm --help
│   ├── create.md              # az vm create --help
│   ├── list.md
│   ├── show.md
│   └── delete.md
├── disk/                      # az disk … (also Compute — multi-verb extension)
├── group/                     # az group … (Resources)
├── login/, config/, extension/, deployment/

src/PS/                        # PowerShell — PlatyPS format, one folder per module
├── Compute/
│   ├── help/                  # Author here
│   │   ├── New-AzVM.md
│   │   ├── Get-AzVM.md
│   │   └── …
│   └── en-US/                 # Generated by build.ps1 — do not hand-edit
│       └── Microsoft.Azure.Azclips.Cmdlets.Compute.dll-Help.xml
├── Config/, Accounts/, Resources/
```

The CLI folder name is the **verb** (the first user-facing CLI token: `vm`, `disk`, `group`, `login`, `config`, `extension`, `deployment`). Verbs are declared by `[ExtensionVerb("...")]` attributes on the extension's `IExtensionIndex` class. One extension may contribute several verbs — for example, `Compute` owns both `vm` and `disk`. The scaffolder uses these verb names to bootstrap templates, so the on-disk layout stays in sync with code at authoring time.

## CLI Markdown Format

Every CLI help file opens with YAML frontmatter, then a `Description:` paragraph, then one or more example sections:

````markdown
---
summary: Create a virtual machine and its networking stack in a single call.
status: ga
schema: 1.0.0
online version:
---

Description: Provisions the full networking + VM stack as a single
operation. Creates a virtual network, network security group, network
interface, and virtual machine using the supplied names and parameters.

### Create a VM with its networking stack

```bash
az vm create --name myvm --location eastus --resource-group myrg --subscription-id 00000000-0000-0000-0000-000000000000
```
````

**Frontmatter fields:**

| Field             | Required | Notes                                                   |
| ----------------- | -------- | ------------------------------------------------------- |
| `summary:`        | yes      | Max ~80 chars. Used in command listings.                |
| `status:`         | no       | `ga` (default), `preview`, `deprecated`, or `internal`. |
| `schema:`         | yes      | Always `1.0.0`.                                         |
| `online version:` | no       | MSLearn URL or empty.                                   |
| `relatedLinks:`   | no       | Flat list of related `az ...` invocations.              |

**Example fences must use `bash` or `sh`.** PowerShell fences are not consumed from CLI markdown — author those in `src/PS/<Module>/help/` instead.

## PowerShell Markdown Format

PowerShell help is authored against `Microsoft.PowerShell.PlatyPS` 1.0.1's current schema (`PlatyPS schema version: 2024-05-01`), which carries metadata as YAML frontmatter and per-parameter YAML blocks. PlatyPS refreshes syntax and parameter metadata from the cmdlet binary. Synopsis, description, examples, and parameter descriptions are author-owned; fill in descriptions for newly added parameters after refreshing. Shape:

````markdown
---
document type: cmdlet
external help file: Microsoft.Azure.Azclips.Cmdlets.Compute.dll-Help.xml
HelpUri: ""
Locale: en-US
Module Name: Compute
ms.date: 06/04/2026
PlatyPS schema version: 2024-05-01
title: New-AzVM
---

# New-AzVM

## SYNOPSIS

Create a virtual machine and its networking stack in a single call.

## SYNTAX

### Default

```
New-AzVM -Name <string> -Location <string> -ResourceGroupName <string> -SubscriptionId <string> [<CommonParameters>]
```

## DESCRIPTION

Provisions the full networking + VM stack …

## EXAMPLES

### Example 1: Create a VM

```powershell
New-AzVM -SubscriptionId 00000000-0000-0000-0000-000000000000 -Name myvm -Location eastus -ResourceGroupName myrg
```

## PARAMETERS

### -Name

The name of the virtual machine.

```yaml
Type: System.String
DefaultValue: ""
SupportsWildcards: false
Aliases: []
ParameterSets:
    - Name: (All)
      Position: Named
      IsRequired: true
      ValueFromPipeline: false
      ValueFromPipelineByPropertyName: false
      ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ""
```

…
````

Legacy `schema: 2.0.0` markdown is still accepted as input by PlatyPS 1.0.1 (`Import-MarkdownCommandHelp` reads both), but new files should use the current schema — that's what `New-PSHelpMarkdown.ps1` emits.

## Example Validation

`Azclips.Help.Tool` validates all examples at build time against the live extension assemblies and cmdlet binaries.

### Markdown structure (both surfaces)

These rules apply uniformly to CLI markdown under `src/CLI/Help/Docs/` and PowerShell markdown under `src/PS/<Module>/help/`:

- **`MD_TodoPlaceholder`** (CLI) / **`PS_TodoPlaceholder`** (PS) — the scaffolder seeds `TODO -` markers in `summary`, `Description`, and example bodies (CLI) and across `SYNOPSIS`, `DESCRIPTION`, parameter blocks, and example bodies (PS). Any remaining `TODO` token in author-owned content is an error. Replace each marker with real prose. Distinct ProblemIds make CLI vs PS failures greppable in build logs.
- **`CLI_ExamplePlaceholder`** / **`PS_ExamplePlaceholder`** — see [Placeholder rules](#placeholder-rules) below.

### CLI fences (`bash` / `sh`) — `src/CLI/Help/Docs/`

Validation errors when:

- An `az ...` line uses an unregistered verb (e.g. `az widget create`).
- The remaining tokens don't resolve to a routed command.
- A `--flag` (or short `-x`) isn't declared on the resolved command and isn't one of the documented globals (`--help`/`-h`, `--debug`, `--output`).
- A flag value doesn't match the declared `ParameterMetadata.Type` or isn't in `AllowedValues`.
- A parameter declared `IsRequired: true` on the routed command is missing from the invocation (`CLI_L4_MissingRequiredFlag`). Help-flag invocations (`az vm create --help`) are exempt.
- An example fence contains an `<angle-bracket>` placeholder of any shape (`CLI_ExamplePlaceholder`). See [Placeholder rules](#placeholder-rules) for the accepted escape hatches.

### PowerShell fences (`powershell` / `pwsh`) — `src/PS/<Module>/help/`

Validation rules (full list, see Troubleshooting below for fixes):

- **PSL1** — `Verb-Noun` doesn't resolve to a registered cmdlet.
- **PSL2** — `-Parameter` isn't declared on the cmdlet, isn't an `[Alias]`, and isn't a common parameter.
- **PSL3** — a parameter declared `Mandatory = true` in `AllParameterSets` is missing.
- **PSL4** — the same parameter (after alias resolution) is bound twice on one invocation.
- **PSL5** — a `$variable` is read but never assigned in the same fence.
- **PSL6** — a literal value bound to a `Guid`- or enum-typed parameter doesn't parse.
- **PSL7** — a fence under `## EXAMPLES` does not invoke the cmdlet whose name matches the file (e.g. an `az …` line in `Connect-AzAccount.md`).
- **PSL8** — the cmdlet help file has no fenced PowerShell example under `## EXAMPLES`.
- **PSL9** — a cmdlet declared in `<Module>.psd1`'s `CmdletsToExport` has no matching `help/<Verb-Noun>.md` file.
- **PSL10** — a module manifest declares `CmdletsToExport` but the module folder has no `help/` directory at all (supersedes the per-cmdlet PSL9 cascade).
- **PSL11** — a `help/<File>.md` exists but no cmdlet of that name is exported from the manifest (warning, not error — may be in-progress work).
- **PSL12** — a PlatyPS `{{ Fill ... }}` placeholder remains in the markdown (e.g. `{{ Fill in the Description }}`, `{{ Fill -Param Description }}`). The PlatyPS-evergreen `{{Insert list of aliases}}` boilerplate is exempt.
- **PSL16** — a Markdown link has an empty destination. Supply a URL, relative path or fragment, or remove the entry. Optional sections can remain empty; inline code and fenced code are excluded.
- **`PS_ExamplePlaceholder`** — an `<angle-bracket>` placeholder appears inside an example fence. PlatyPS-required `<string>`, `<CommonParameters>`, etc. inside `## SYNTAX` are not scanned. See [Placeholder rules](#placeholder-rules) below.

Only fences inside `## EXAMPLES` are scanned, and only when the language tag is `powershell`, `pwsh`, `ps`, or empty. Fences in `## SYNTAX` / `## PARAMETERS` (PlatyPS pseudocode) are deliberately ignored. PSL9–PSL11 walk the manifest ↔ filesystem contract directly and don't depend on fence content.

### Placeholder rules

`<angle-bracket>` placeholders inside example fences (`bash` / `sh` for CLI, `powershell` / `pwsh` for PS) are rejected. Type-name shapes (`<string>`, `<int>`, `<guid>`) communicate nothing useful to the reader, and domain-shaped placeholders (`<my-rg>`, `<subscription-id>`) are no better — both push validation burden onto users. Author runnable examples instead.

Accepted in fences:

- Realistic literal values: `myrg`, `eastus`, `Standard_D2s_v5`, `azureuser`.
- All-zero sentinel GUID for any subscription/tenant/resource id: `00000000-0000-0000-0000-000000000000`.
- Shell variables: `$rg`, `${RG_VAR}`, `$env:LOCATION`, `$script:foo` (PS).
- Splatted hashtables in PowerShell: `Get-Azclips @params` skips parameter validation entirely.

Rejected in fences:

- Type-name placeholders: `<string>`, `<int>`, `<guid>`, `<bool>`.
- Domain placeholders: `<my-rg>`, `<subscription-id>`, `<name>`, `<location>`.

Prose outside fences is not scanned — you can still mention `<key>=<value>` in a `Description:` paragraph if it aids readability.

### Conventions

- Use canonical kebab-case for CLI flags (`--subscription-id`, not `--subscriptionId`).
- Both `--flag value` and `--flag=value` are accepted.
- Splatted PowerShell commands (`Get-Azclips @params`) skip parameter validation.

## Runtime Behavior

### Help Routing (Discovery-Backed)

Azclips uses a **discovery-backed model** for help resolution:

1. **Root help** (`az --help`) runs a discovery pass to enumerate all installed extensions, then renders help for each discovered verb. No static hint table needed.
2. **Verb help** (`az vm --help`) resolves the owning package by querying what's currently installed via discovery. If no installed extension owns the verb, the system falls back to an embedded hint table to suggest which package to install.
3. **Command help** (`az vm create --help`) works the same way — owned by the discovered extension.

This means help is always in sync with what's actually installed. If you drop a new extension into `~/.azclips/extensions/`, the next `az --help` immediately shows its verbs without any registry update required.

### CLI Help

`az <verb> <command> --help` is routed at runtime:

1. Azclips runs a discovery pass to find all installed extensions and their contributed verbs.
2. It looks up which extension owns the requested verb from the discovered metadata.
3. It loads the owning extension's help provider and renders the markdown for that verb + command.

If the verb is not found in discovered extensions, a known error lists the owning package (from the embedded hint table) and suggests `az extension add --name <Package>`.

Help markdown is stored in `src/CLI/Help/Docs/<verb>/<command>.md`. The build copies these files to `artifacts/<Config>/Core.Cli/help/`, and at runtime `MarkdownCommandHelpProvider` parses Summary/Description/examples on demand. If the packaged file is missing during development, the provider walks up to `azclips.slnx` and reads the markdown straight from `src/CLI/Help/Docs/<verb>/` — dev-loop fallback, no rebuild required.

### PowerShell Help

`Get-Help <Cmdlet>` reads MAML XML from `<ModuleBase>/<culture>/<dll>-Help.xml`. The build:

1. Runs `Import-MarkdownCommandHelp` over `src/PS/<Module>/help/*.md` and pipes through `Export-MamlCommandHelp` to write MAML XML. The produced `<Module>-Help.xml` is then renamed to whatever the per-cmdlet `external help file:` frontmatter declares (e.g. `Microsoft.Azure.Azclips.Cmdlets.Accounts.dll-Help.xml`) and copied into both `src/PS/<Module>/en-US/` (gitignored local build output) and `artifacts/Debug/PS/<Module>/en-US/` (next to the staged `.psd1`).
2. Copies that XML to `artifacts/<Config>/PS/<Module>/en-US/` so `Import-Module artifacts/<Config>/PS/<Module>/<Module>.psd1` resolves help next to the manifest.

## Build Process

### Standard build (no help validation)

```powershell
dotnet build azclips.slnx
```

### Full help pipeline

```powershell
.\build\build.ps1 -GenerateHelpArtifacts -Configuration Debug
```

This:

1. Builds the solution.
2. Runs `build/Install-BuildDependencies.ps1` (pins `Microsoft.PowerShell.PlatyPS` to `1.0.1` if missing).
3. Runs `Azclips.Help.Tool`:
    - `MarkdownStructureValidator` — frontmatter shape.
    - `HelpCompletenessValidator` — every CLI command in `IExtensionIndex` has a markdown file.
    - `ExampleSyntaxValidator` — CLI fences resolve.
    - `PowerShellExampleSyntaxValidator` — PS fences resolve against cmdlet binaries; prose checks catch placeholders and empty links (PSL1–PSL8, PSL12, PSL16, `PS_TodoPlaceholder`, `PS_ExamplePlaceholder`).
    - `PsCompletenessValidator` — every cmdlet in `<Module>.psd1`'s `CmdletsToExport` has a `help/<Verb-Noun>.md` file (PSL9–PSL11).
    - Emits `artifacts/help/<extension>/HelpManifest.json` and `artifacts/help/HelpIndex.json`.
4. Runs `Import-MarkdownCommandHelp | Export-MamlCommandHelp` per module → `src/PS/<Module>/en-US/<dll>-Help.xml` (renamed from PlatyPS's default `<Module>-Help.xml` to match the `external help file:` frontmatter), then copies into the staged `artifacts/Debug/PS/<Module>/en-US/`.

The build fails non-zero if any validator emits errors.

## Scaffolding New Extensions

```powershell
# CLI markdown stubs for one verb at a time (the verb's owning extension is auto-resolved)
dotnet run --project tools/Help/Azclips.Help.Tool -- --scaffold <verb>

# PS markdown stubs from cmdlet binary
.\build\New-PSHelpMarkdown.ps1 -Module <Module>
```

Both are idempotent: existing files are skipped. Run them once after adding new commands or cmdlets, then hand-fill the templates. Note: the CLI scaffolder operates on a verb (`vm`, `disk`, `group`), not an extension. Multi-verb extensions (e.g. `Compute`, which owns both `vm` and `disk`) require one `--scaffold` call per verb.

## Testing Help

```powershell
# CLI surface
.\build\build.ps1 -GenerateHelpArtifacts
dotnet .\artifacts\Debug\Core.Cli\az.dll vm --help
dotnet .\artifacts\Debug\Core.Cli\az.dll vm create --help

# PowerShell surface
Import-Module .\artifacts\Debug\PS\Compute\Compute.psd1 -Force
Get-Help New-AzVM -Full
Get-Help Get-AzVM -Examples

# Automated tests
dotnet test tests/Cli.Tests/Cli.Tests.csproj
dotnet test tools/Help/Azclips.Help.Tool.Tests/Azclips.Help.Tool.Tests.csproj
```

## Best Practices

1. **Keep examples concise** — show the command and key arguments, not every flag.
2. **Use realistic, runnable values** — `myrg` for resource group names, `eastus` for locations, all-zero GUID (`00000000-0000-0000-0000-000000000000`) for any subscription/tenant/resource id. Angle-bracket placeholders (`<my-rg>`, `<string>`) are rejected by `CLI_ExamplePlaceholder` / `PS_ExamplePlaceholder` — see [Placeholder rules](#placeholder-rules).
3. **One example per task** — "Login with subscription ID", not "All ways to login".
4. **Edit markdown, never code** — no `[Summary]` attributes, no inline help strings in processors.
5. **Run the validators locally** — `.\build\build.ps1 -GenerateHelpArtifacts` runs the same pipeline CI runs.
6. **Don't hand-edit `en-US/*.xml`** — those are PlatyPS output and will be overwritten on the next build.

## Troubleshooting

**`Get-Help` returns empty `REMARKS`:**

- The MAML XML wasn't deployed next to the cmdlet DLL. Run `.\build\build.ps1 -GenerateHelpArtifacts`.
- Confirm the XML landed: `Get-ChildItem artifacts\Debug\PS\<Module>\en-US\`.
- `Remove-Module <Module>` and re-`Import-Module ... -Force` — PowerShell caches the help-not-found result per session.

**CLI help shows no examples:**

- Check that the code fence has a `bash` or `sh` language tag.
- Ensure `### Example Title` precedes the code block.
- Verify the markdown filename matches the canonical command key (`group create` → `create.md` inside `src/CLI/Help/Docs/group/`).

**Build fails with `CLI_L1_*` / `CLI_L2_*` / `CLI_L3_*` / `CLI_L4_*` errors:**

- `L1`: command path doesn't resolve. Cross-check against `ExtensionIndex.cs`.
- `L2`: unknown flag. Check the canonical name (kebab-case).
- `L3`: value doesn't match the declared type. Substitute the all-zero GUID for ids, a real string literal (`eastus`, `myrg`), or a shell variable (`$rg`). Angle-bracket placeholders are no longer accepted — see `CLI_ExamplePlaceholder`.
- `CLI_L4_MissingRequiredFlag`: an `IsRequired: true` parameter on the routed command isn't bound in the invocation. Add the missing `--flag`. Help-flag invocations (`az vm create --help`) are exempt.

**Build fails with `MD_TodoPlaceholder` / `PS_TodoPlaceholder`:**

- The scaffolder seeded `TODO -` markers in `summary`, `Description`, example titles, or example bodies. Search the file for `TODO` and replace each marker with real prose. PowerShell scaffolds also seed the `OUTPUTS` section with a `TODO - describe what <cmdlet> does` line — fill that in too.

**Build fails with `CLI_ExamplePlaceholder` / `PS_ExamplePlaceholder`:**

- An example fence contains an `<angle-bracket>` placeholder (`<string>`, `<my-rg>`, `<subscription-id>`, etc.). Replace each with a realistic literal (`eastus`, `myrg`, `Standard_D2s_v5`), the all-zero sentinel GUID for ids, or a shell variable (`$rg`, `${RG_VAR}`). Splatted PowerShell hashtables (`Get-Azclips @params`) are also accepted. See [Placeholder rules](#placeholder-rules) for the full list.

**Build fails with `PSL1_*` / `PSL2_*` errors:**

- `PSL1`: cmdlet name isn't registered. Check the `Verb-Noun` spelling against the cmdlet class's `[Cmdlet]` attribute.
- `PSL2`: unknown `-Parameter`. The parameter isn't declared on the cmdlet (and isn't an `[Alias]` or a common parameter).

**Build fails with `PSL3_*` / `PSL4_*` / `PSL5_*` / `PSL6_*` errors:**

- `PSL3` (missing required parameter): the cmdlet declares the parameter as `Mandatory = true` in `AllParameterSets`. Add the missing flag, or — if the example splatts (`@params`) — that automatically suppresses the check.
- `PSL4` (duplicate parameter): the same parameter (or one of its aliases) is bound twice on the same invocation. Remove the duplicate.
- `PSL5` (unassigned variable): a `$variable` is read inside the fence but never assigned. Either assign it earlier in the same fence (`$rg = "my-rg"`), use a drive-qualified form (`$env:NAME`, `$script:foo`), or splat (`@params`) — the validator skips splatted reads.
- `PSL6` (invalid parameter value): a literal value bound to a `Guid`- or enum-typed parameter does not parse. Substitute the all-zero GUID (`00000000-0000-0000-0000-000000000000`), an enum member name, or a `$variable`. Variable, sub-expression, and interpolated-string values are not checked. Angle-bracket placeholders (`<guid>`, `<your-id>`) are rejected by `PS_ExamplePlaceholder`.

**Build fails with `PSL7_ExampleMustInvokeDocumentedCmdlet`:**

- The validator extracts the documented cmdlet from the filename (`Connect-AzAccount.md` → `Connect-AzAccount`) and requires every fence under `## EXAMPLES` to invoke it at least once. Setup commands in the same fence are fine — what's not fine is a fence that demonstrates a different cmdlet, or worse, a CLI invocation (`az ...`) inside a PowerShell help file.
- Fix: replace the example so it actually shows the cmdlet the file documents.

**Build fails with `PSL8_NoExamples`:**

- The cmdlet help file has zero fenced code blocks under `## EXAMPLES`. Empty `### <Title>` headings are not enough — they render as a blank section in `Get-Help -Examples`. PlatyPS-scaffolded stubs land here when an author hasn't filled them in.
- Fix: add at least one `### <Title>` + fenced `powershell` block that demonstrates the cmdlet.

**Build fails with `PSL9_MissingHelpFile` / `PSL10_MissingHelpFolder` / `PSL11_OrphanedHelpFile`:**

- `PSL9` (missing help file): a cmdlet declared in `<Module>.psd1`'s `CmdletsToExport` has no matching `help/<Verb-Noun>.md`. Run `.\build\New-PSHelpMarkdown.ps1 -Module <Module>` to scaffold the missing file (existing files are skipped), then author it.
- `PSL10` (missing help folder): the manifest declares cmdlets but the module folder has no `help/` directory at all. Same fix — the scaffolder creates the folder. PSL10 supersedes the per-cmdlet PSL9 cascade so the build log isn't drowned.
- `PSL11` (orphaned help file): a `.md` file exists for a cmdlet that's no longer in `CmdletsToExport`. This is a warning, not an error — it covers in-progress drafts. To resolve: either add the cmdlet to the manifest or delete the file. Common cause: the cmdlet was renamed/removed and the help file was forgotten.

**Build fails with `PSL12_FillPlaceholder`:**

- A PlatyPS `{{ Fill ... }}` placeholder remains in the markdown (e.g. `{{ Fill in the Description }}`, `{{ Fill -SubscriptionId Description }}`, `{{ Fill in the NOTES }}`). PlatyPS seeds these when the cmdlet binary lacks the corresponding metadata; replace each with real prose. The PlatyPS-evergreen `{{Insert list of aliases}}` line is exempt and may stay.

**Note on fence scanning:** the PowerShell validator only walks fences inside `## EXAMPLES` and only when the language tag is `powershell`, `pwsh`, `ps`, or empty (PlatyPS's default). Fences in `## SYNTAX`, `## PARAMETERS`, or any other section are deliberately ignored — those are PlatyPS pseudocode (`<String>` placeholders, YAML metadata) and are not real PowerShell.

**Build fails locking `Core.Abstractions.dll` or other artifacts:**

- A pwsh session has imported one of the cmdlet modules. Close it (`Remove-Module Compute`, etc.) or exit the pwsh window before rebuilding.

**`Microsoft.PowerShell.PlatyPS` not found / wrong version:**

- Run `.\build\Install-BuildDependencies.ps1` directly. It pins to `1.0.1`. The build script invokes this automatically; if you bypass it, you may end up with a different version. The legacy `platyPS` 0.14.2 module is no longer used.
