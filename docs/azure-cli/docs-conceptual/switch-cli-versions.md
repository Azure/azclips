# Switch between the classic Azure CLI and the preview build

By default the installer keeps preview `az` isolated and off your `PATH`, so it coexists with the
**classic** `azure-cli`. Adding preview `az` to `PATH` permanently makes it **shadow** the classic
`azure-cli` in every new shell.

If you want to keep both installed and flip between them **within the same session** — for example to
compare behaviour or fall back to the classic CLI — add one of the helper functions below to your shell
profile. Calling `switch` (bash / zsh) or `Switch-Az` (PowerShell) toggles the azcli directory on and
off the front of `PATH` and prints which `az` is now active.

These functions assume the default install locations:

| Platform | azcli (preview) location |
| --- | --- |
| Linux / macOS | `~/.local/share/azcli/` |
| Windows | `%LOCALAPPDATA%\Programs\azcli\` |

If you installed azcli to a custom location, update the path in the function accordingly.

## bash / zsh

Add the function to your shell profile (`~/.bashrc` or `~/.zshrc`), then reload it:

```bash
# Toggle az between the classic CLI and the azcli preview build (current shell only)
switch() {
  local preview_dir="$HOME/.local/share/azcli"
  local current
  current=$(command -v az)

  if [[ "$current" == "$preview_dir/az" ]]; then
    # Preview is active -> drop it from PATH so the classic CLI takes over
    export PATH="${PATH#$preview_dir:}"
  else
    # Classic (or nothing) is active -> put the preview at the front of PATH
    export PATH="$preview_dir:$PATH"
  fi

  command -v az
}
```

```bash
source ~/.bashrc   # or: source ~/.zshrc
```

## PowerShell

Add the function to your PowerShell profile (`$PROFILE`), then reload it:

```powershell
# Toggle az between the classic CLI and the azcli preview build (current session only)
function Switch-Az {
    $previewDir = Join-Path $env:LOCALAPPDATA 'Programs\azcli'
    $previewAz  = Join-Path $previewDir 'az.exe'
    $sep        = [IO.Path]::PathSeparator

    $current = Get-Command az -CommandType Application -ErrorAction SilentlyContinue |
        Select-Object -First 1 -ExpandProperty Source

    if ($current -and (Split-Path -Parent $current) -eq $previewDir) {
        # Preview is active -> drop it from PATH so the classic CLI takes over
        $env:PATH = ($env:PATH -split [regex]::Escape($sep) |
            Where-Object { $_ -and $_ -ne $previewDir }) -join $sep
    }
    else {
        # Classic (or nothing) is active -> put the preview at the front of PATH
        if (-not (Test-Path $previewAz)) {
            Write-Error "Switch-Az: preview az not found at '$previewAz'"
            return
        }
        $env:PATH = "$previewDir$sep$env:PATH"
    }

    $active = Get-Command az -CommandType Application -ErrorAction SilentlyContinue |
        Select-Object -First 1 -ExpandProperty Source
    if ($active) { $active } else { 'Switch-Az: az is not on PATH' }
}
```

```powershell
if (-not (Test-Path $PROFILE)) { New-Item -ItemType File -Path $PROFILE -Force | Out-Null }
. $PROFILE
```

## Try it

Run the toggle and confirm which `az` is active:

```bash
switch          # bash / zsh
which az        # -> ~/.local/share/azcli/az when the preview is active
```

```powershell
Switch-Az               # PowerShell
Get-Command az          # -> ...\Programs\azcli\az.exe when the preview is active
```

Each call flips to the other CLI for the **current shell only** — open a new shell (or call the
function again) to return to your default.

## Related

| You want to… | Guide |
| --- | --- |
| Install the Azure CLI preview build | [docs/install-overview.md](./install-overview.md) |
| Install the Azure PowerShell preview modules | [docs/install-powershell-modules.md](../../azure-powershell/docs-conceptual/install-powershell-modules.md) |
| Report a problem or share feedback | [Log an issue on GitHub](https://github.com/Azure/azclips/issues) |
