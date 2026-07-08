# az Tab Completion

## How It Works

1. **Build time**: `build/build.ps1` builds the solution and copies shell completion scripts to `artifacts/<Configuration>/Core.Cli/`.
2. **Runtime**: Shell completion scripts (`.ps1`, `.bash`, `.zsh`) dynamically invoke `az --completions [group] [subcommand]` at tab time, ensuring completions always match whichever `az` binary is on PATH.

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

Build the solution first:
```powershell
.\build\build.ps1 -Configuration Release
```

### PowerShell

```powershell
. <path-to>/az.completions.ps1

# Set env var to enable azclips detection (skip system Azure CLI)
$env:AZCLIPS_BINARY = '1'

# Optional: alias az so you can test without full path
Set-Alias az <path-to>\az.exe

# Test completions
az <Tab>                              # top-level groups
<path-to>\az.exe <Tab>                # path-qualified also works
az vm create --<Tab>                  # parameters
```

### bash

```bash
export AZCLIPS_BINARY=1
source <path-to>/az.completions.bash

# Optional: add az to PATH so you can test without full path
export PATH="<path-to-az-directory>:$PATH"

az <Tab>              # top-level groups
az vm <Tab>           # subcommands
az vm create --<Tab>  # parameters
```

### zsh

```zsh
export AZCLIPS_BINARY=1
autoload -U compinit && compinit
source <path-to>/az.completions.zsh

# Optional: add az to PATH so you can test without full path
export PATH="<path-to-az-directory>:$PATH"

az <Tab>              # top-level groups
az vm <Tab>           # subcommands
az vm create --<Tab>  # parameters
```

### Notes

- `$AZCLIPS_BINARY=1` tells the scripts this is azclips, not system Azure CLI. Without it, bare `az` completions are skipped.
- Completion results are cached per session. Run `_azClearCache` (PS) or `_az_clear_cache` (bash/zsh) to refresh.
- Requires `jq` for bash/zsh.

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
