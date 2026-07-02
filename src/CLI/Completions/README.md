# az Tab Completion

## How It Works

1. **Build time**: `build/build.ps1` builds the solution and copies shell completion scripts to `bin/`.
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

### PowerShell

```powershell
# Build solution
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
# Build solution
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
_azGetCompletions "az group " 9
```

## Files

| File | Purpose |
|---|---|
| `CompletionGenerator.cs` | `--completions` handler; builds JSON tree from extension metadata (called at tab time) |
| `az.completions.ps1` | PowerShell completer; queries `az --completions` dynamically + `TabExpansion2` override |
| `az.completions.bash` | Bash completer; queries `az --completions` dynamically |
| `az.completions.zsh` | Native zsh completer using `compdef`; queries `az --completions` dynamically |
