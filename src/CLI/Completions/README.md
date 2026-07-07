# az Tab Completion

## How It Works

1. **Build time**: `build/build.ps1` builds the solution and copies shell completion scripts to `bin/`.
2. **Runtime**: Shell completion scripts (`.ps1`, `.bash`, `.zsh`) dynamically invoke `az --completions [group] [subcommand]` at tab time, ensuring completions always match whichever `az` binary is configured.

### Binary Resolution

Each script needs to know which `az` binary is the azclips CLI. The resolution order is:

| Priority | Method | Applies to |
|---|---|---|
| 1 | **User-provided path** (bash/zsh: argument to `source`; PS: automatic via regex) | bash, zsh |
| 2 | **Script directory** — looks for `az` or `az.exe` next to the script | all |
| 3 | **Repo artifacts** — walks up from script dir to find `global.json`, then checks `artifacts/{Release,Debug}/Core.Cli/az(.exe)` | all |

**Bare `az` guard**: When the user types bare `az <Tab>` (no path prefix):
- **PowerShell**: runs `az --version` on the resolved binary and checks that output does NOT contain `azure-cli` (system Azure CLI).
- **bash/zsh**: compares the `realpath` of the PATH `az` against the configured azclips binary. If they differ, completions are skipped.

Path-qualified invocations (e.g. `./az <Tab>`, `/path/to/az <Tab>`) always complete.

The completion tree structure returned by `az --completions`:

```json
{
  "group": {
    "_subcommands": ["create", "list"],
    "_params": ["--verbose"],
    "create": ["--name", "--location"],
    "list": ["--subscription"]
  }
}
```

## Quick Start

### PowerShell

```powershell
# Build solution
.\build\build.ps1

# Load completions into current session
. .\artifacts\Release\Core.Cli\az.completions.ps1

# Test with bare az (if azclips is on PATH)
az <Tab>

# Test with path-qualified invocation (always works)
.\artifacts\Release\Core.Cli\az.exe <Tab>
# Or without .exe extension (PowerShell resolves via PATHEXT)
.\artifacts\Release\Core.Cli\az <Tab>
```

### bash (e.g. WSL)

```bash
# Option 1: Pass the azclips binary path explicitly
source src/CLI/Completions/az.completions.bash /mnt/c/path/to/artifacts/Release/Core.Cli/az.exe

# Option 2: Auto-detect from script directory or repo artifacts
source artifacts/Release/Core.Cli/az.completions.bash

# If azclips is on PATH, bare az completes:
az <Tab>

# If system Azure CLI is on PATH instead, add azclips to PATH first:
export PATH="/path/to/artifacts/Release/Core.Cli:$PATH"
source src/CLI/Completions/az.completions.bash
az <Tab>
```

### zsh

```zsh
# Option 1: Pass the azclips binary path explicitly
autoload -U compinit && compinit
source src/CLI/Completions/az.completions.zsh /path/to/artifacts/Release/Core.Cli/az.exe

# Option 2: Auto-detect from script directory or repo artifacts
autoload -U compinit && compinit
source artifacts/Release/Core.Cli/az.completions.zsh

# If azclips is on PATH, bare az completes:
az <Tab>
```

**Note**: bash/zsh `complete`/`compdef` registers by command name (`az`, `az.exe`), so
path-qualified invocations like `/full/path/to/az.exe <Tab>` do NOT trigger completions.
Use bare `az` with PATH set correctly, or use an alias:
```bash
alias az='/path/to/artifacts/Release/Core.Cli/az.exe'
az <Tab>  # works
```

## Toggling Completion

Completion is enabled by default. A config key gates it at the source of truth
(`az --completions`), so you can turn it off without un-sourcing the script:

```bash
# Disable — az --completions returns an empty tree, so no shell offers suggestions
az config set --key completion.enabled --value false

# Re-enable
az config set --key completion.enabled --value true
```

## Testing Completions Manually

```powershell
# After loading the script, call the internal function directly:
_azGetCompletions "az group " 9
```

## Files

| File | Purpose |
|---|---|
| `CompletionGenerator.cs` | `--completions` handler; builds JSON tree from extension metadata (called at tab time) |
| `az.completions.ps1` | PowerShell completer; queries `az --completions` dynamically + `TabExpansion2` override |
| `az.completions.bash` | Bash completer; queries `az --completions` dynamically |
| `az.completions.zsh` | Native zsh completer using `compdef`; queries `az --completions` dynamically |
