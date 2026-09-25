# az Tab Completion

## How It Works

1. **Build time**: `build/build.ps1` builds the solution and copies shell completion scripts to `artifacts/<Configuration>/Core.Cli/`.
2. **Runtime**: Shell completion scripts query the resolved `az` binary and cache command metadata for the current shell session.
   - **PowerShell** uses `az --completions [command-group]` (or no args for full tree), which returns a JSON tree parsed with built-in `ConvertFrom-Json`.
   - **bash/zsh** use `az --completion-candidates [command-path...]` which returns a plain-text word list (no JSON parser required).
   - **Saved failure IDs** use `az --completion-values troubleshoot --id` (plus `--json` for PowerShell). This reads the non-sensitive local ID/timestamp index on each request, without keys, history writes, authentication, or network access.

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

# Optional: choose your preferred PSReadLine completion style yourself
Set-PSReadLineKeyHandler -Key Tab -Function MenuComplete

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

- PowerShell detects azclips from JSON returned by `az --completions`. Bash/Zsh use the existing `--completion-candidates` uppercase-error heuristic: lowercase word lists are candidates, while empty output produces no candidates. Detection is cached per executable for the shell session.
- Completion results are cached per session. Run `_azClearCache` (PS) or `_az_clear_cache` (bash/zsh) to refresh.
- The PowerShell script registers native argument completions and chains through the existing `TabExpansion2` fallback; it does not change PSReadLine key bindings.
- **No external dependencies** for bash/zsh (no `jq` required).

## Saved failure IDs

The existing completers offer complete IDs only in these new value positions:

```text
az troubleshoot --id <Tab>
az troubleshoot --id=<Tab>
az troubleshoot show --id <Tab>
az troubleshoot show --id=<Tab>
```

IDs look like `42-2026-0924-0000-0002`: parent-shell PID, UTC date/minute, and a durable
counter. These candidates are never stored in the static command cache. The endpoint
returns complete candidates; PowerShell's existing wildcard matching, Bash `compgen`,
and Zsh `compadd` apply their normal matching settings. There is no separate fuzzy search
or server-side prefix filter. Labels contain only IDs and timestamps, never saved commands
or error text. Exact execution still requires a full ID.

Completion does not verify the encrypted record or unlock its key. A record can also be evicted
between completion and execution; an unavailable exact ID is an error, never an implicit fallback.
Dynamic IDs follow the current `AZCLIPS_HOME` on each lookup. The static command tree retains its
existing cache rules; refresh that cache after switching state homes when needed.

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
$line = 'az group '
_azGetCompletions -exePath (Get-Command az).Source -inputText $line -cursorPos $line.Length
```

Offline shell regression tests use fake executable responses, not the user's CLI or history:

```powershell
pwsh -NoProfile -File tests\Cli.Tests\Recovery\TroubleshootCompletion.Tests.ps1
pwsh -NoProfile -File tests\Cli.Tests\Recovery\CompletionNonRegression.Tests.ps1
```

```bash
bash tests/Cli.Tests/Recovery/TroubleshootCompletion.Tests.bash
```

The compatibility tests compare ordinary inputs with pre-feature commit `83f27105`,
including whitespace, `--key=value`, candidate order, cache/fallback behavior and query
counts. PowerShell also compares labels and replacement ranges. Running the Bash tests
under Git Bash verifies that shell, not Linux/macOS or Zsh.

## Files

| File | Purpose |
|---|---|
| `CompletionGenerator.cs` | `--completions` (JSON) and `--completion-candidates` (plain text) handlers; builds completion data from extension metadata |
| `az.completions.ps1` | PowerShell native argument completer; queries `az --completions` (JSON), preserves existing `TabExpansion2` fallback, and does not change PSReadLine key bindings |
| `az.completions.bash` | Bash completer; queries `az --completion-candidates` (plain text, no jq) |
| `az.completions.zsh` | Native zsh completer using `compdef`; queries `az --completion-candidates` (plain text, no jq) |
