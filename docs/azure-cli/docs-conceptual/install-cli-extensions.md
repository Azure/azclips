---
title: Install Azure CLI public preview extensions
description: Learn how to install add-on extensions for the Azure CLI public preview.
---

# Install Azure CLI public preview extensions

> **Most users should start with the [install guide](install-overview.md).** This page covers add-on extensions, which you only need on **lean** installs or to pin a specific extension version.

Add-on extensions ship as NuGet packages named `azcli.extensions.<name>.<version>.nupkg`. You need a working azcli before you can add extensions — follow the [install guide](install-overview.md) to install the **lean** or **full** package first, then use the steps below.

> The **full** azcli package already bundles the add-on extensions — you only need this guide if you installed the **lean** package or want a specific extension version from a package file.

> **First, make sure `az` points at azcli in this shell.** Because azcli installs in isolation (PATH is not modified by default), add it to `PATH` for the session before running the `az extension` commands below — otherwise `az` may resolve to the classic azure-cli:
>
> ```bash
> export PATH="$HOME/.local/share/azcli:$PATH"     # Linux / macOS — current shell only
> ```
>
> ```powershell
> $env:Path = "$env:LOCALAPPDATA\Programs\azcli;$env:Path"   # Windows — current session only
> ```
>
> Or skip this and call azcli by its full path (e.g. `~/.local/share/azcli/az extension add …`).

## Install from the extensions bundle

Each release includes a single `azcli-extensions-<version>.tar.gz` containing every extension package plus `NOTICE`/`LICENSE`. This bundle is how add-on extensions are distributed.

Download `azcli-extensions-<version>.tar.gz` from the public [GitHub Releases](https://github.com/Azure/azclips/releases) page, choosing the same release you installed azcli from. No GitHub account or token is required.

For the latest release, use the commands for your shell below. For an older release, use its download page instead.

The bundle unpacks as loose `.nupkg` files, so extract it into a folder and install from there. Extension package versions are independent of the release version, so the commands below don't hardcode a version — follow the set for your shell.

### Linux / macOS / WSL (bash)

Requires Bash, `curl`, `tar`, and standard system text utilities:

```bash
(
set -euo pipefail
release=$(curl -fsSL https://api.github.com/repos/Azure/azclips/releases/latest)
if ! bundle_url=$(printf '%s' "$release" | tr -d '\r\n' |
  grep -oE '"browser_download_url"[[:space:]]*:[[:space:]]*"https://github[.]com/Azure/azclips/releases/download/[A-Za-z0-9._-]+/azcli-extensions-[0-9][A-Za-z0-9._-]*[.]tar[.]gz"' |
  sed 's/^[^:]*:[[:space:]]*"//; s/"$//'); then
  echo "Could not locate the extensions bundle download URL." >&2
  exit 1
fi
if [[ "$bundle_url" == *$'\n'* ]]; then
  echo "Expected exactly one extensions bundle download URL." >&2
  exit 1
fi
curl -fL "$bundle_url" -o azcli-extensions.tar.gz
mkdir -p azcli-extensions
tar -xzf ./azcli-extensions.tar.gz -C azcli-extensions

# Install one extension (e.g. compute)
az extension add --path ./azcli-extensions/azcli.extensions.compute.*.nupkg

# Or install every bundled extension
for pkg in ./azcli-extensions/azcli.extensions.*.nupkg; do
  az extension add --path "$pkg"
done

az extension list
)
```

### Windows (PowerShell)

PowerShell doesn't expand `*` for external commands, so resolve the paths explicitly with `Get-ChildItem`:

```powershell
$release = Invoke-RestMethod https://api.github.com/repos/Azure/azclips/releases/latest -ErrorAction Stop
$assets = @($release.assets | Where-Object name -Like 'azcli-extensions-*.tar.gz')
if ($assets.Count -ne 1) { throw 'Expected exactly one extensions bundle.' }
Invoke-WebRequest $assets[0].browser_download_url -OutFile ./azcli-extensions.tar.gz -ErrorAction Stop
New-Item -ItemType Directory -Force -Path azcli-extensions | Out-Null
tar -xzf ./azcli-extensions.tar.gz -C azcli-extensions
if ($LASTEXITCODE -ne 0) { throw 'Extensions bundle extraction failed.' }

# Install one extension (e.g. compute)
$compute = Get-ChildItem azcli-extensions/azcli.extensions.compute.*.nupkg | Select-Object -First 1
az extension add --path $compute.FullName

# Or install every bundled extension
Get-ChildItem azcli-extensions/azcli.extensions.*.nupkg | ForEach-Object { az extension add --path $_.FullName }

az extension list
```

## Verify

```bash
az extension list
```

The extension you added should appear in the list. If the command it provides now runs, the extension is installed.

## Manage extensions

| Task | Command |
| --- | --- |
| List installed extensions | `az extension list` |

## Troubleshooting

| Symptom | Fix |
| --- | --- |
| `az` is not found | Install azcli first — see the [install guide](install-overview.md). |
| A command says an extension is missing | Install the add-on from the extensions bundle with `az extension add --path <pkg>` (see above). |
| `--path` package not found | Confirm the file name and version match your download, e.g. `azcli.extensions.compute.0.1.0.nupkg`. |
| Extension already installed | Use `az extension update --name <name>` or remove then re-add it. |
