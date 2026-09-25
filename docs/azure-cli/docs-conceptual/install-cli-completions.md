---
title: Enable Azure CLI private preview tab completion
description: Learn how to enable shell tab completion for the Azure CLI private preview in bash, zsh, and PowerShell.
---

# Enable Azure CLI private preview tab completion

Azure CLI private preview ships shell completion scripts for **bash**, **zsh**, and **PowerShell**. Once enabled, pressing `<Tab>` completes command groups, subcommands, and parameters for the `az` command. Command metadata is loaded from `az` and cached in the current shell session. Saved failure IDs are queried afresh in their value positions.

## Where the scripts live

The completion scripts are included in the CLI archive under `completions/`. After a default install they are at:

| Platform | Default location |
| --- | --- |
| Linux / macOS | `~/.local/share/azcli/completions/` |
| Windows | `%LOCALAPPDATA%\Programs\azcli\completions\` |

Files: `az.completions.bash`, `az.completions.zsh`, `az.completions.ps1`. If you installed azcli to a custom location, use `<install-root>/completions/` instead.

## Enable it

### bash

```bash
# Try it in the current shell
source ~/.local/share/azcli/completions/az.completions.bash

# Make it permanent (and load it now)
echo 'source ~/.local/share/azcli/completions/az.completions.bash' >> ~/.bashrc
source ~/.bashrc
```

### zsh

```zsh
# Ensure the completion system is initialised
autoload -U compinit && compinit

# Try it in the current shell
source ~/.local/share/azcli/completions/az.completions.zsh

# Make it permanent (and load it now)
echo 'autoload -U compinit && compinit' >> ~/.zshrc
echo 'source ~/.local/share/azcli/completions/az.completions.zsh' >> ~/.zshrc
source ~/.zshrc
```

### PowerShell

```powershell
# Try it in the current session
. "$env:LOCALAPPDATA\Programs\azcli\completions\az.completions.ps1"

# Make it permanent (adds to your PowerShell profile)
if (-not (Test-Path $PROFILE)) { New-Item -ItemType File -Path $PROFILE -Force | Out-Null }
Add-Content -LiteralPath $PROFILE '. "$env:LOCALAPPDATA\Programs\azcli\completions\az.completions.ps1"'
. $PROFILE
```

## Try it

```text
az <Tab>              # top-level groups
az vm <Tab>           # subcommands
az vm create --<Tab>  # parameters
az troubleshoot --id <Tab>  # saved failure IDs
az troubleshoot show --id=<Tab>  # exact IDs for local details
```

Failure IDs are queried afresh, separately from the cached command tree. These suggestions contain
only IDs and timestamps: they do not start AI, call Azure, decrypt error details, or unlock the
local key store. IDs have the form `<PID>-<yyyy>-<MMdd>-<HHmm>-<counter>`. The shell's existing
matching rules still apply; no separate fuzzy matching or new Tab interaction is introduced.
Use `az troubleshoot --list` to view summaries, or `az troubleshoot show --id <failure-id>`
to read the complete saved command and error locally.
Selecting a suggested ID still requires an exact, available record; completion does not verify
decryption or prevent a record from being evicted before the command runs.

## Turn completion on or off

Completion is enabled by default. You can toggle it through config without un-sourcing the script — the CLI is the source of truth, so a disabled setting makes every shell stop offering suggestions:

```bash
az config set --key completion.enabled --value false   # disable
az config set --key completion.enabled --value true    # enable
```

## Refresh and troubleshoot

| Symptom | Fix |
| --- | --- |
| No suggestions appear | Confirm `az` resolves to azcli (`which az` / `Get-Command az`) and that you sourced the script for your shell. |
| Stale suggestions after updating azcli | Refresh the session cache: `_az_clear_cache` (bash/zsh) or `_azClearCache` (PowerShell). |
| zsh: `compdef: command not found` | Add `autoload -U compinit && compinit` before sourcing the script. |
| Want to see what's happening (zsh) | `export AZ_COMPLETION_DEBUG=1` (log written to `/tmp/az_completion_debug.log`). |
