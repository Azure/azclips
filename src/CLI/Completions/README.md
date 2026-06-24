# az Tab Completion

## How It Works

1. **Build time**: `build/build.ps1` runs `az --completions`, which introspects all loaded extensions and outputs a JSON command tree to `bin/az.completions.json`.
2. **Runtime**: Shell completion scripts (`.ps1`, `.bash`, `.zsh`) load the same JSON tree and register tab completion for `az`.

The completion tree structure:

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
# Build solution + generate completion data
.\build\build.ps1

# Load completions into current session
. .\bin\az.completions.ps1
```

Now press Tab after `az ` to see suggestions:
```powershell
az group <Tab>
```

### zsh

```zsh
# Build solution + generate completion data
pwsh ./build/build.ps1

# Load completions into current session
autoload -U compinit
compinit
source ./bin/az.completions.zsh
```

Now press Tab after `az ` to see suggestions:
```zsh
az group <Tab>
```

## Testing Completions Manually

```powershell
# After loading the script, call the internal function directly:
_azGetCompletions "az group " 11
```

## Files

| File | Purpose |
|---|---|
| `CompletionGenerator.cs` | `--completions` handler; builds JSON tree from extension metadata |
| `az.completions.ps1` | PowerShell completer; loads JSON + registers `TabExpansion2` override |
| `az.completions.bash` | Bash completer (same JSON data source) |
| `az.completions.zsh` | Native zsh completer using `compdef` (same JSON data source) |
