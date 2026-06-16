# azv2 Tab Completion

## How It Works

1. **Build time**: `build/build.ps1` runs `azv2 --completions`, which introspects all loaded extensions and outputs a JSON command tree to `bin/azv2.completions.json`.
2. **Runtime**: Shell completion scripts (`.ps1`, `.bash`, `.zsh`) load the same JSON tree and register tab completion for `azv2`.

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
. .\bin\azv2.completions.ps1
```

### Bash

```bash
# Build solution + generate completion data
pwsh ./build/build.ps1

# Load completions into current session
source ./bin/azv2.completions.bash
```

### zsh

```zsh
# Build solution + generate completion data
pwsh ./build/build.ps1

# Load completions into current session
autoload -U compinit
compinit
source ./bin/azv2.completions.zsh
```

Now press Tab after `azv2 ` to see suggestions:

```text
azv2 group <Tab>
```

## Testing Completions Manually

### PowerShell

```powershell
# After loading the script, call the internal function directly:
_Azv2GetCompletions "azv2 group " 11
```

### zsh — unit test (no live binary needed)

The test script mocks `compadd` and the `$words`/`$CURRENT` completion variables
so it runs fully offline against a fixture JSON file:

```zsh
zsh tests/Scripts/CLI/zsh-completions.zsh
```

Expected output (all green):

```text
=== Level 0: completing command groups ===
  PASS groups: suggests 'group' — found 'group'
  PASS groups: suggests 'login' — found 'login'
...
--- Results: 13 total, 13 passed, 0 failed ---
```

### zsh — interactive smoke test

Load the script into a live shell and trigger Tab:

```zsh
autoload -U compinit && compinit
source ./bin/azv2.completions.zsh

azv2 <Tab>            # → group  login  …
azv2 group <Tab>      # → create  list  --verbose
azv2 group create <Tab>   # → --name  --location  --subscription-id
```

To inspect what candidates `_azv2` would pass to `compadd` without actually
replacing the line, use the zsh `_compadd` debug wrapper:

```zsh
# Temporarily mock compadd and call _azv2 directly
compadd() { print "compadd args: $*"; }
words=(azv2 group create) CURRENT=4
_azv2
unfunction compadd   # restore
```

## Files

| File | Purpose |
|---|---|
| `CompletionGenerator.cs` | `--completions` handler; builds JSON tree from extension metadata |
| `azv2.completions.ps1` | PowerShell completer; loads JSON + registers `TabExpansion2` override |
| `azv2.completions.bash` | Bash completer (same JSON data source) |
| `azv2.completions.zsh` | Native zsh completer using `compdef` (same JSON data source) |
