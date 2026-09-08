# az Tab Completion

## How It Works

1. **Build time**: `build/build.ps1` builds the solution and copies shell completion scripts to `artifacts/<Configuration>/Core.Cli/`.
2. **Runtime**: Shell completion scripts dynamically invoke `az` at tab time, ensuring completions always match whichever binary is on PATH.
   - **PowerShell** uses `az --completions [command-group]` (or no args for full tree), which returns a JSON tree parsed with built-in `ConvertFrom-Json`.
   - **bash/zsh** use `az --completion-candidates [command-path...]` which returns a plain-text word list (no JSON parser required).

The JSON tree structure returned by `az --completions` (used by PowerShell):

```json
{
  "deployment": {
    "_subcommands": ["group create", "group what-if"],
    "group create": ["--name", "--resource-group", "--template-file"],
    "group what-if": ["--name", "--resource-group", "--result-format"]
  }
}
```

The plain-text output returned by `az --completion-candidates` (used by bash/zsh):

```
$ az --completion-candidates           # top-level groups
vm storage network config ...

$ az --completion-candidates vm        # subcommands + params
create list show delete --verbose ...

$ az --completion-candidates vm create # params
--name --location --size ...
```

## Quick Start

Build the solution first:
```powershell
.\build\build.ps1 -Configuration Release
```

### PowerShell

```powershell
. <path-to>/az.completions.ps1

# Optional: alias az so you can test without full path
Set-Alias az <path-to>\az.exe

# Test completions
az <Tab>                              # top-level groups
<path-to>\az.exe <Tab>                # path-qualified also works
az vm create --<Tab>                  # parameters
```

### bash

```bash
source <path-to>/az.completions.bash

# Optional: add az to PATH so you can test without full path
export PATH="<path-to-az-directory>:$PATH"

az <Tab>              # top-level groups
az vm <Tab>           # subcommands
az vm create --<Tab>  # parameters
```

### zsh

```zsh
autoload -U compinit && compinit
source <path-to>/az.completions.zsh

# Optional: add az to PATH so you can test without full path
export PATH="<path-to-az-directory>:$PATH"

az <Tab>              # top-level groups
az vm <Tab>           # subcommands
az vm create --<Tab>  # parameters
```

### Notes

- The scripts auto-detect azclips vs old Azure CLI using the output of `az --completion-candidates`: azclips returns lowercase word lists (or empty when disabled); old Azure CLI returns error text containing uppercase letters (or empty via stderr suppression). The detection result is cached per exe per session.
- Completion results are cached per session. Run `_azClearCache` (PS) or `_az_clear_cache` (bash/zsh) to refresh.
- **No external dependencies** for bash/zsh (no `jq` required).

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
| `CompletionGenerator.cs` | `--completions` (JSON) and `--completion-candidates` (plain text) handlers; builds completion data from extension metadata |
| `az.completions.ps1` | PowerShell completer; queries `az --completions` (JSON) + `TabExpansion2` override |
| `az.completions.bash` | Bash completer; queries `az --completion-candidates` (plain text, no jq) |
| `az.completions.zsh` | Native zsh completer using `compdef`; queries `az --completion-candidates` (plain text, no jq) |
