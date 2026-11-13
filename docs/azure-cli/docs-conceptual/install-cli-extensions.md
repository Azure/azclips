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

Download `azcli-extensions-<version>.tar.gz` from [GitHub Releases](https://github.com/Azure/azclips/releases), choosing the same release you installed azcli from.

The download-only helpers below fetch the latest bundle and save it as `azcli-extensions.tar.gz` in the current directory. They do not extract or install extensions. If your CLI is from an older release, use the release-pinned examples below or download its bundle manually.

The bundle unpacks as loose `.nupkg` files, so extract it into a folder and install from there. Extension package versions are independent of the release version, so the commands below don't hardcode a version — follow the set for your shell.

### Linux / macOS / WSL (bash)

Requires Bash, `curl`, `tar`, and standard system text utilities:

**1. Download and extract**

Run each command only after the previous one succeeds:

```bash
# Linux / macOS / WSL
curl -fsSL https://raw.githubusercontent.com/Azure/azclips/main/install/azcli-extensions-download.sh | bash
mkdir -p azcli-extensions
tar -xzf ./azcli-extensions.tar.gz -C azcli-extensions
```

For a specific release, replace the download command with:

```bash
curl -fsSL https://raw.githubusercontent.com/Azure/azclips/main/install/azcli-extensions-download.sh | bash -s -- --release '<release-tag>'
```

**2. Install extensions**

Choose **one** of the following options.

Install one extension (for example, Compute):

```bash
az extension add --path ./azcli-extensions/azcli.extensions.compute.*.nupkg
```

Or install every bundled extension:

```bash
for pkg in ./azcli-extensions/azcli.extensions.*.nupkg; do
  az extension add --path "$pkg"
done
```

### Windows (PowerShell)

PowerShell doesn't expand `*` for external commands, so resolve the paths explicitly with `Get-ChildItem`:

**1. Download and extract**

Run each command only after the previous one succeeds:

```powershell
# Windows (PowerShell 7+)
irm https://raw.githubusercontent.com/Azure/azclips/main/install/azcli-extensions-download.ps1 -ErrorAction Stop | iex
New-Item -ItemType Directory -Force -Path azcli-extensions | Out-Null
tar -xzf ./azcli-extensions.tar.gz -C azcli-extensions
if ($LASTEXITCODE -ne 0) { throw 'Extensions bundle extraction failed.' }
```

For a specific release, download the helper and pass the tag explicitly:

```powershell
Invoke-WebRequest https://raw.githubusercontent.com/Azure/azclips/main/install/azcli-extensions-download.ps1 -OutFile ./azcli-extensions-download.ps1 -ErrorAction Stop
./azcli-extensions-download.ps1 -Release '<release-tag>'
```

**2. Install extensions**

Choose **one** of the following options.

Install one extension (for example, Compute):

```powershell
$compute = Get-ChildItem azcli-extensions/azcli.extensions.compute.*.nupkg | Select-Object -First 1
az extension add --path $compute.FullName
```

Or install every bundled extension:

```powershell
Get-ChildItem azcli-extensions/azcli.extensions.*.nupkg | ForEach-Object { az extension add --path $_.FullName }
```

## Verify

In either shell, list the installed extensions:

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
