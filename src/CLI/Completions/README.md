# azv2 Tab Completion

## How It Works

1. **Build time**: `build/build.ps1` runs `azv2 --completions`, which introspects all loaded extensions and outputs a JSON command tree to `bin/azv2.completions.json`.
2. **Runtime**: `azv2.completions.ps1` loads the JSON tree and registers a PowerShell tab completer for `azv2`.

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

```powershell
# Build solution + generate completion data
.\build\build.ps1

# Load completions into current session
. .\bin\azv2.completions.ps1

# Now press Tab after "azv2 " to see suggestions
azv2 group <Tab>
```

## Testing Completions Manually

```powershell
# After loading the script, call the internal function directly:
_Azv2GetCompletions "azv2 group " 11
```

## Files

| File | Purpose |
|---|---|
| `CompletionGenerator.cs` | `--completions` handler; builds JSON tree from extension metadata |
| `azv2.completions.ps1` | PowerShell completer; loads JSON + registers `TabExpansion2` override |
| `azv2.completions.bash` | Bash completer (same JSON data source) |
