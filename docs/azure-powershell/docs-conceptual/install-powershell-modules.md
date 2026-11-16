---
title: Install the Azure PowerShell public preview release
description: Learn how to install the Az PowerShell modules from the Azure PowerShell public preview release.
---

# Install the Azure PowerShell public preview release

> This page covers **azps** (the Az PowerShell modules) — a separate product from azcli. For the CLI, see the [Install Overview](../../azure-cli/docs-conceptual/install-overview.md).

The Az PowerShell cmdlets ship as NuGet module packages (`Az.*.nupkg`). Each release includes `azps-cmdlets-<version>.tar.gz`, which contains:

- the `Az.<version>.nupkg` roll-up (meta) package,
- every `Az.*` dependency package it requires,
- an `InstallModule.ps1` helper, and
- `NOTICE` / `LICENSE`.

## Requirements

| Requirement | Why |
| --- | --- |
| **PowerShell 7.6 or later** | The binary cmdlets are built on `System.Management.Automation` 7.6 (.NET 10). PowerShell 7.5 and Windows PowerShell 5.1 cannot load them. |

Check your version with `$PSVersionTable.PSVersion`. Install or update PowerShell from <https://aka.ms/powershell> if needed.

## Install with the bootstrap

All steps below run in **PowerShell 7.6+**.

The bootstrap downloads the latest release and invokes its bundled module installer. `tar` must be available on `PATH`.

```powershell
# Linux / macOS / WSL / Windows (PowerShell 7.6+)
irm https://raw.githubusercontent.com/Azure/azclips/main/install/azps-install.ps1 -ErrorAction Stop | iex
```

The bootstrap installs the `Az` roll-up and its dependencies for the current user by default. It uses a unique temporary local PSRepository and removes that repository and the extracted files afterward.

### Alternative: download and run

Use this optional workflow to inspect the script before running it or to pass installer parameters.
Download the script:

```powershell
# Linux / macOS / WSL / Windows (PowerShell 7.6+)
Invoke-WebRequest https://raw.githubusercontent.com/Azure/azclips/main/install/azps-install.ps1 -OutFile ./azps-install.ps1 -ErrorAction Stop
```

After a successful download, review the script, then run:

```powershell
./azps-install.ps1
```

### Bootstrap options

| Parameter | Default | Purpose |
| --- | --- | --- |
| `-ModuleName` | `Az` | Install a single module instead of the roll-up, e.g. `-ModuleName Az.Compute`. |
| `-RequiredVersion` | highest packaged version of the module selected by `-ModuleName` | Pin a specific version. |
| `-Scope` | `CurrentUser` | Use `AllUsers` for a machine-wide install (needs elevation). |
| `-RepositoryName` | unique `AzclipsInstall-...` name | Name of the temporary local PSRepository; must not already exist. |

Example — install just the Compute module for all users:

```powershell
./azps-install.ps1 -ModuleName Az.Compute -Scope AllUsers
```

## Manual / offline installation

Download `azps-cmdlets-<version>.tar.gz` from [GitHub Releases](https://github.com/Azure/azclips/releases). Use a fresh extraction directory for each release:

```powershell
New-Item -ItemType Directory -Path azps-cmdlets | Out-Null
$bundle = Get-Item ./azps-cmdlets-<version>.tar.gz
tar -xzf $bundle.FullName -C azps-cmdlets
if ($LASTEXITCODE -ne 0) { throw 'PowerShell bundle extraction failed.' }
./azps-cmdlets/InstallModule.ps1
```

Replace `<version>` with the downloaded filename's version. The bundled helper accepts the same module/version/scope parameters, but its repository-name default is `LocalAzclips`. After installation, run `Unregister-PSRepository -Name LocalAzclips` before removing the extracted directory.

## Verify

After installing the roll-up, start a fresh PowerShell 7.6+ session and explicitly load the preview:

```powershell
Import-Module Az -MinimumVersion 0.1.0 -MaximumVersion 0.1.9999
Get-Module -Name Az | Select-Object Name, Version
```

The loaded `Az` version should be in the `0.1.*` range. If you installed a single module instead, replace `Az` in both commands with that module's name, such as `Az.Compute`.

The preview installs side by side with classic Azure PowerShell. An unversioned `Import-Module Az` loads the highest installed version, which is classic Az when both are installed. Running a cmdlet before explicitly importing the preview can also autoload classic modules. To change versions, start a new session; see [Switch between classic and preview Azure PowerShell](./switch-azps-versions.md).

After importing the preview, cmdlets that contact Azure require sign-in (`Connect-AzAccount`) and a subscription.

## Troubleshooting

| Symptom | Fix |
| --- | --- |
| Module fails to import / load error | Confirm PowerShell 7.6+ (`$PSVersionTable.PSVersion`). 7.5 and 5.1 cannot load these cmdlets. |
| `Register-PSRepository` says the repo already exists | Run `Unregister-PSRepository -Name LocalAzclips`, then retry (the bundled script does this automatically). |
| `Install-Module` cannot find the module | Run it from the extracted `azps-cmdlets` folder, or pass the correct `-Repository`/`SourceLocation` path. |
| Classic Az loads instead of the preview | Start a fresh PowerShell 7.6+ session and explicitly import the preview with the version constraint shown above before running any Az cmdlets. |
| Need a machine-wide install | Use `-Scope AllUsers` from an elevated PowerShell session. |
| Release lookup returns HTTP 403 or 429 | Check GitHub's response for an API rate limit; retry after it resets, or download the bundle manually from GitHub Releases. |
| Bootstrap says the repository already exists | Choose a different `-RepositoryName`; the bootstrap does not replace existing repositories. |
