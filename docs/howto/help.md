# Adding Help Content

This guide explains how to author and test help for CLI commands and PowerShell cmdlets in Cirrus.

## Quick Start

1. **Create markdown help file:**

    ```
    src/Extensions/{Extension}/help/{command}.md
    ```

2. **Write help content:**

    ````markdown
    Summary: Brief one-line description

    Description: Multi-line description explaining what the command does
    and how to use it.

    ### Example Title

    ```bash
    azv2 login --subscription-id 00000000-0000-0000-0000-000000000000
    ```
    ````

    ### PowerShell Example Title

    ```powershell
    Connect-CirrusAccount -SubscriptionId "00000000-0000-0000-0000-000000000000"
    ```

    ```

    ```

3. **Remove old help metadata** from `ExtensionIndex.cs`:
    - Delete `Summary`, `Description`, `Examples` from command entries

4. **Build and test:**
    ```powershell
    dotnet build cirrus.slnx
    dotnet .\artifacts\Debug\Core.Cli\azv2.dll {command} --help
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

### Summary (Required)

```markdown
Summary: One-line description of the command
```

- Appears in command listings and brief help
- Max ~80 characters

### Description (Required)

```markdown
Description: Multi-paragraph explanation of what the command does,
when to use it, and any important notes.

Can span multiple lines and paragraphs.
```

- Appears in full help output
- Plain text, no special formatting

### Examples (Required)

Examples are code blocks with a title:

````markdown
### Example Title

```bash
azv2 login --subscription-id 00000000-0000-0000-0000-000000000000
```
````

### Another Example

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

### Example Validation (CLI)

CLI examples are mechanically validated at build time by `tools/Help/Cirrus.Help.Tool` against the live `IExtensionIndex`. The tool fails the build if:

- An `azv2 ...` line uses a CLI group that isn't registered (e.g. `azv2 widget create`).
- The remaining tokens don't resolve to a routed command (e.g. `azv2 group bogus`).
- A `--flag` (or short `-x`) is not declared on the resolved command and isn't one of the documented globals: `--help` / `-h`, `--debug`, `--output`.

Conventions:

- Use the canonical kebab-case flag form: `--subscription-id`, not `--subscriptionId`.
- The `--flag=value` form is accepted alongside `--flag value`.
- Placeholder values are passed through unchanged. Common conventions:
    - `<name>`, `<my-rg>`, `<placeholder>` — angle-bracket placeholder.
    - `$var`, `${var}` — shell-style variable reference.
    - All-zero GUID (`00000000-0000-0000-0000-000000000000`) — generic stand-in for a subscription/tenant/resource id.
- PowerShell fences (` ```powershell ` / ` ```pwsh `) are not validated against CLI metadata — they're surfaced through `Get-Help` and `.dll-Help.xml` artifacts.

> **Required-flag presence** (e.g. "every `--name` example must also include `--subscription-id`") is **not yet** enforced — it will land alongside positional parameter metadata. Examples that omit required flags are accepted today but should still be kept honest by reviewers.

## Full Example

**File:** `src/Extensions/Login/help/index.md`

```markdown
Summary: Authenticate to Azure and set the current account context.

Description: Log in to your Azure subscription to authenticate Cirrus CLI
and PowerShell commands. You can specify a subscription ID and tenant ID,
or use device code flow for headless environments.

### Login with subscription ID
```bash
azv2 login --subscription-id 00000000-0000-0000-0000-000000000000
````

### Login with tenant ID

```bash
azv2 login --subscription-id 00000000-0000-0000-0000-000000000000 --tenant-id 11111111-1111-1111-1111-111111111111
```

### Login with PowerShell

```powershell
Connect-CirrusAccount -SubscriptionId "00000000-0000-0000-0000-000000000000"
```

### Login with device code flow

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
artifacts/help/powershell/Cirrus.Login.dll-Help.xml
```

When you run:

```powershell
Get-Help Connect-CirrusAccount -Full
```

PowerShell reads from the XML artifact (includes both CLI and PowerShell examples).

## Build Process

### Standard Build

```powershell
dotnet build cirrus.slnx
```

- Compiles all projects
- Loads markdown help at **runtime** via `MarkdownCommandHelpProvider`

### Build with Help Artifacts

```powershell
.\build\build.ps1 -GenerateHelpArtifacts -BuildConfiguration Debug
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
Import-Module .\artifacts\Debug\Cirrus.Login.psd1
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

1. **Keep examples concise** — Show the command and key arguments, not exhaustive flags
2. **Use realistic values** — Replace placeholders with `00000000-0000-0000-0000-000000000000` for GUIDs
3. **One example per task** — "Login with subscription ID", not "All ways to login"
4. **Write for both surfaces** — Provide both CLI and PowerShell examples if the command exists in both
5. **Update markdown, not code** — Don't add help in `[Summary]` attributes or processor Summary fields
6. **Test after changes** — Run help and visually check output after editing markdown
7. **Validate syntax** — Ensure Summary/Description/Example blocks follow the format exactly

## Migrating an Extension to Markdown Help

If you're moving an existing extension from code-embedded help to markdown:

1. **Create help folder:**

    ```powershell
    mkdir src/Extensions/{Extension}/help
    ```

2. **Extract help from code:**
    - Copy `Summary` from processor attributes into `src/Extensions/{Extension}/help/index.md`
    - Convert `Examples` to markdown code blocks with language tags

3. **Update ExtensionIndex.cs:**
    - Remove `Summary`, `Description`, `Examples` from CLI command metadata
    - Keep the routing and command structure intact

4. **Clean up processors:**
    - Remove `[Summary]` and other help attributes from processor classes

5. **Run tests:**

    ```powershell
    dotnet test tests/Cli.Tests/Cli.Tests.csproj
    ```

    - Should pass with no help-related failures
    - If failures occur, check that markdown file names match command keys in ExtensionIndex

## Troubleshooting

**Help shows no examples:**

- Check that code fence has a language tag: ` ```bash ` or ` ```powershell `
- Ensure `### Example Title` precedes the code block
- Verify file path matches command routing in ExtensionIndex

**PowerShell help missing:**

- Run `.\build\build.ps1 -GenerateHelpArtifacts` to generate XML artifacts
- Check that `artifacts/help/powershell/{Module}.dll-Help.xml` exists
- Verify PowerShell examples use ` ```powershell ` or ` ```pwsh ` tag

**CLI shows PowerShell examples:**

- Help system filters ` ```powershell ` code blocks from CLI output
- If seeing PowerShell examples in CLI, file a bug (likely a language-tag parsing issue)

**Build fails during artifact generation:**

- Check syntax of markdown (Summary:/Description:/Examples)
- Verify all code blocks have matching ``` delimiters
- Run `dotnet build cirrus.slnx` first to ensure DLLs are built
