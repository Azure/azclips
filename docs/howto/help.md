# Adding Help Content

This guide explains how to author and test help for CLI commands and PowerShell cmdlets in Cirrus.

## Quick Start

1. **Create a markdown help file** at `src/Extensions/<Extension>/help/<command>.md` (use `index.md` for the root command of a group).

2. **Write the content** — YAML frontmatter, then a description, then one or more example sections:

   ````markdown
   ---
   summary: Brief one-line description.
   status: ga
   schema: 1.0.0
   online version:
   ---

   Description: Multi-line explanation of what the command does and how to use it.

   ### Login with subscription id (CLI)

   ```bash
   azv2 login --subscription-id 00000000-0000-0000-0000-000000000000
   ```

   ### Connect with subscription id (PowerShell)

   ```powershell
   Connect-CirrusAccount -SubscriptionId "00000000-0000-0000-0000-000000000000"
   ```
   ````

3. **Build and verify CLI help:**

   ```powershell
   dotnet build cirrus.slnx
   dotnet .\artifacts\Debug\Core.Cli\azv2.dll <command> --help
   ```

4. **Build with help artifacts** (required for `Get-Help` to find PowerShell help XML and for CI-style validation locally):

   ```powershell
   .\build\build.ps1 -GenerateHelpArtifacts -Configuration Debug
   Import-Module .\src\PS\<Module>\<Module>.psd1 -Force
   Get-Help <Cmdlet> -Full
   ```

## File Structure

Help markdown files live in:

```
src/Extensions/
├── Login/
│   └── help/
│       ├── index.md          # Root login command
│       ├── logout.md         # Logout subcommand
│       └── show.md           # Show context subcommand
├── Config/
│   └── help/                 # (create as needed)
├── Resources/
│   └── help/                 # (create as needed)
└── Extension/
    └── help/                 # (create as needed)
```

For **root commands** (like `login`), use `help/index.md`.
For **subcommands** (like `login logout`), use `help/logout.md`.

## Markdown Format

Each help file contains:

### Frontmatter (Required)

Every help file opens with a YAML frontmatter block delimited by `---` on its own line. See §4 (Authoring Model) in [docs/spec/help-system.md](../spec/help-system.md) for the full schema.

```yaml
---
summary: One-line description of the command. # required
status: ga # optional, default 'ga'; enum: ga|preview|deprecated|internal
schema: 1.0.0 # required
online version: # optional, MSLearn URL (empty for now)
relatedLinks: # optional, flat list
    - azv2 logout
---
```

- `summary:` appears in command listings and brief help. Max ~80 characters.
- `status:` flags the command's lifecycle. `internal` hides from public listings.
- `schema:` lets us version the format if we ever need a breaking change.

### Description (Required)

```markdown
Description: Multi-paragraph explanation of what the command does,
when to use it, and any important notes.

Can span multiple lines and paragraphs.
```

- Appears in full help output
- Plain text, no special formatting

### Examples (Required)

Examples are code blocks with a title. Add a `(CLI)` or `(PowerShell)` suffix to disambiguate which surface the example targets:

````markdown
### Example Title (CLI)

```bash
azv2 login --subscription-id 00000000-0000-0000-0000-000000000000
```
````

### Another Example (PowerShell)

```powershell
Connect-CirrusAccount -SubscriptionId "00000000-0000-0000-0000-000000000000"
```

````

**Language tags:**
- `bash` or `sh` → CLI example (shown in `azv2 --help`)
- `powershell` or `pwsh` → PowerShell example (shown in `Get-Help` and XML artifacts)

Each example must have:
1. A `### Title` heading (describes what the example does)
2. A code fence with language tag
3. The command(s) to execute

### Example Validation

Examples are mechanically validated at build time by `tools/Help/Cirrus.Help.Tool` against the live `IExtensionIndex` (CLI) and reflected cmdlet metadata (PowerShell). The tool fails the build if any of the following are wrong; the live validators run as part of `.\build\build.ps1 -GenerateHelpArtifacts` and in the `Validate Help Content` step of [build-and-test.yml](../../.github/workflows/build-and-test.yml).

**CLI fences (`bash` / `sh`):**

- An `azv2 ...` line uses a CLI group that isn't registered (e.g. `azv2 widget create`).
- The remaining tokens don't resolve to a routed command (e.g. `azv2 group bogus`).
- A `--flag` (or short `-x`) is not declared on the resolved command and isn't one of the documented globals: `--help` / `-h`, `--debug`, `--output`.
- A flag value doesn't match the declared parameter type (e.g. `--subscription-id not-a-guid`) or isn't in the parameter's `AllowedValues` list.

**PowerShell fences (`powershell` / `pwsh`):**

- A `Verb-Noun` doesn't resolve to a registered cmdlet (e.g. `Set-Unknown -Key foo`).
- A `-Parameter` is not declared on the cmdlet and isn't an `[Alias]` or a common parameter (`-Verbose`, `-ErrorAction`, etc.).

**Conventions:**

- Use the canonical kebab-case flag form (`--subscription-id`, not `--subscriptionId`).
- Both `--flag value` and `--flag=value` are accepted.
- Placeholders pass through unchanged:
    - `<name>`, `<my-rg>` — angle-bracket placeholder.
    - `$var`, `${var}` — shell-style variable reference.
    - All-zero GUID (`00000000-0000-0000-0000-000000000000`) — generic stand-in for any subscription/tenant/resource id.
- Splatted PowerShell commands (`Get-Cirrus @params`) skip parameter validation (the hashtable contents aren't statically visible).

> **Required-flag presence** (e.g. "every `--name` example must also include `--subscription-id`") is **not yet** enforced — it will land alongside positional parameter metadata. Examples that omit required flags are accepted today but should still be kept honest by reviewers.

## Full Example

**File:** `src/Extensions/Login/help/index.md`

```markdown
---
summary: Authenticate to Azure and set the current account context.
status: ga
schema: 1.0.0
online version:
---

Description: Log in to your Azure subscription to authenticate Cirrus CLI
and PowerShell commands. You can specify a subscription ID and tenant ID,
or use device code flow for headless environments.

### Login with subscription ID (CLI)
```bash
azv2 login --subscription-id 00000000-0000-0000-0000-000000000000
````

### Login with tenant ID (CLI)

```bash
azv2 login --subscription-id 00000000-0000-0000-0000-000000000000 --tenant-id 11111111-1111-1111-1111-111111111111
```

### Login with subscription ID (PowerShell)

```powershell
Connect-CirrusAccount -SubscriptionId "00000000-0000-0000-0000-000000000000"
```

### Login with device code flow (CLI)

```bash
azv2 login --subscription-id 00000000-0000-0000-0000-000000000000 --use-device-code
```

````

## Runtime Behavior

### CLI Help

When you run:
```bash
dotnet .\artifacts\Debug\Core.Cli\azv2.dll login --help
````

The runtime:

1. Loads the markdown file from `src/Extensions/Login/help/index.md`
2. Parses Summary/Description
3. Filters examples to CLI-only (ignores `powershell`/`pwsh` code blocks)
4. Renders formatted help output

### PowerShell Help

PowerShell help is pre-generated during build into:

```
artifacts/help/powershell/Microsoft.Azure.Cirrus.Cmdlets.<Module>.dll-Help.xml
```

`.\build\build.ps1 -GenerateHelpArtifacts` copies that XML into `src/PS/<Module>/en-US/` (the directory PowerShell searches), handling manifests that use either `RootModule` or `NestedModules`. After that, `Get-Help <Cmdlet> -Full` reads from the XML artifact natively.

## Build Process

### Standard Build

```powershell
dotnet build cirrus.slnx
```

- Compiles all projects
- Loads markdown help at **runtime** via `MarkdownCommandHelpProvider`

### Build with Help Artifacts

```powershell
.\build\build.ps1 -GenerateHelpArtifacts -Configuration Debug
```

- Runs standard build first
- Invokes `tools/Help/Cirrus.Help.Tool/Program.cs` which:
    - Reads all markdown files
    - Parses and validates help content
    - Generates `artifacts/help/{extension}/HelpManifest.json`
    - Generates `artifacts/help/powershell/{Module}.dll-Help.xml`
    - Generates `artifacts/help/HelpIndex.json`

Use `-GenerateHelpArtifacts` for **release builds** or **artifact export**.

## Testing Help

### CLI Help

```powershell
# Root command
dotnet .\artifacts\Debug\Core.Cli\azv2.dll login --help

# Subcommand
dotnet .\artifacts\Debug\Core.Cli\azv2.dll login logout --help
```

### PowerShell Help (after artifact generation)

```powershell
.\build\build.ps1 -GenerateHelpArtifacts -Configuration Debug
Import-Module .\src\PS\Login\Login.psd1 -Force
Get-Help Connect-CirrusAccount -Full
```

### Automated Tests

```powershell
dotnet test tests/Cli.Tests/Cli.Tests.csproj
```

Tests validate:

- Help loads without errors
- Examples are correctly filtered by surface (CLI vs PowerShell)
- Help manifests are properly generated
- PowerShell XML is well-formed

## Best Practices

1. **Keep examples concise** — show the command and key arguments, not every flag.
2. **Use realistic placeholders** — `00000000-0000-0000-0000-000000000000` for any GUID, `<my-rg>` for free-form names.
3. **One example per task** — "Login with subscription ID", not "All ways to login".
4. **Cover both surfaces** — provide both CLI and PowerShell examples if the command is exposed on both.
5. **Edit markdown, never code** — no `[Summary]` attributes, no inline help strings in processors.
6. **Run the validators locally** — `.\build\build.ps1 -GenerateHelpArtifacts` runs the same four-stage validator pipeline CI runs, with file:line locators on every failure.

## Troubleshooting

**Help shows no examples:**

- Check that the code fence has a language tag: ` ```bash ` or ` ```powershell `.
- Ensure `### Example Title` precedes the code block.
- Verify the markdown filename matches the canonical command key (`group create` → `create.md` inside `Group/help/`, multi-word → `dash-separated.md`).

**`Get-Help` returns empty `REMARKS`:**

- The MAML XML wasn't deployed next to the cmdlet DLL. Run `.\build\build.ps1 -GenerateHelpArtifacts` — the script copies `artifacts/help/powershell/*.dll-Help.xml` into `src/PS/<Module>/en-US/`.
- Confirm the XML landed: `Get-ChildItem src/PS/<Module>/en-US/`.

**Build fails with `CLI_L1_*` / `CLI_L2_*` / `CLI_L3_*` errors:**

- `L1`: command path doesn't resolve. The example references a group or subcommand that isn't routed. Cross-check against `ExtensionIndex.cs`.
- `L2`: unknown flag. The flag isn't declared on the resolved command and isn't a global. Check the canonical name (kebab-case) and that it exists on the right command.
- `L3`: value doesn't match the declared type. Either fix the literal in the example or use a placeholder (`<name>`, `$var`, all-zero GUID).

**Build fails with `PSL1_*` / `PSL2_*` errors:**

- `PSL1`: cmdlet name isn't registered. Check the `Verb-Noun` spelling against the cmdlet class's `[Cmdlet]` attribute.
- `PSL2`: unknown `-Parameter`. The parameter isn't declared on the cmdlet (and isn't an `[Alias]` or a common parameter). For splatted forms, use `Cmdlet @params` — splatting is allowlisted.

**Build fails during artifact generation:**

- Confirm frontmatter is well-formed (delimited by `---`, `summary:` and `schema:` present).
- Verify all code fences have matching ` ``` ` delimiters.
- Run `dotnet build cirrus.slnx` first so the extension DLLs are available for reflection.
