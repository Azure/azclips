# Enable azcli tab completion

azcli ships shell completion scripts for **bash**, **zsh**, and **PowerShell**. Once enabled, pressing `<Tab>` completes command groups, subcommands, and parameters for the `az` command. Completions are resolved by calling `az` at tab-time, so they always match the installed CLI — and no `jq` or other dependency is required.

> **Prerequisite — `az` must be on your `PATH`.** Completion is registered for the `az` command, so azcli's `az` has to resolve on your `PATH` (see [Run azcli](../README.md#run-azcli)). If you only run azcli by its full path, tab completion won't apply. Check with `which az` (bash/zsh) or `Get-Command az` (PowerShell).

## Where the scripts live

The completion scripts are included in the CLI archive under `completions/`, so after installing they are at:

| Platform | Path |
| --- | --- |
| Linux / macOS | `~/.local/share/azcli/completions/` |
| Windows | `%LOCALAPPDATA%\Programs\azcli\completions\` |

Files: `az.completions.bash`, `az.completions.zsh`, `az.completions.ps1`.

## Enable it

### bash

```bash
# Try it in the current shell
source ~/.local/share/azcli/completions/az.completions.bash

# Make it permanent
echo 'source ~/.local/share/azcli/completions/az.completions.bash' >> ~/.bashrc
```

### zsh

```zsh
# Ensure the completion system is initialised (once, near the top of ~/.zshrc)
autoload -U compinit && compinit

# Try it in the current shell
source ~/.local/share/azcli/completions/az.completions.zsh

# Make it permanent
echo 'source ~/.local/share/azcli/completions/az.completions.zsh' >> ~/.zshrc
```

### PowerShell

```powershell
# Try it in the current session
. "$env:LOCALAPPDATA\Programs\azcli\completions\az.completions.ps1"

# Make it permanent (adds to your PowerShell profile)
Add-Content $PROFILE '. "$env:LOCALAPPDATA\Programs\azcli\completions\az.completions.ps1"'
. $PROFILE
```

## Try it

```text
az <Tab>              # top-level groups
az vm <Tab>           # subcommands
az vm create --<Tab>  # parameters
```

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
| Want to see what's happening (bash/zsh) | `export AZ_COMPLETION_DEBUG=1` (log written to `/tmp/az_completion_debug.log`). |
