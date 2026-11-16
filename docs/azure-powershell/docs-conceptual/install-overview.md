---
title: Install the Azure PowerShell public preview
description: Learn how to install the Azure PowerShell public preview modules.
---

# Install the Azure PowerShell public preview

The Azure PowerShell public preview ships as **azps** — the Az PowerShell modules (`Az.*`). The modules require PowerShell 7.6 or later.

> [!NOTE]
> For the Azure CLI (`az` command-line tool), see the [Azure CLI public preview install overview](../../azure-cli/docs-conceptual/install-overview.md).

## Install azps (one command)

The installer downloads the latest release from [GitHub Releases](https://github.com/Azure/azclips/releases).

**Prerequisites:**

- [PowerShell 7.6+](https://aka.ms/powershell)
- `tar` available on `PATH` to extract the bundle

**Linux / macOS / WSL / Windows** (run inside PowerShell 7.6+, `pwsh`):

```powershell
# Linux / macOS / WSL / Windows (PowerShell 7.6+)
irm https://raw.githubusercontent.com/Azure/azclips/main/install/azps-install.ps1 -ErrorAction Stop | iex
```

The bootstrap downloads and extracts the bundle into a temporary directory, invokes its `InstallModule.ps1` helper for the current user, and removes its temporary repository and files afterward.

For module/version/scope options or manual/offline installation, see the [module installation guide](./install-powershell-modules.md).

## Verify

Start a fresh PowerShell 7.6+ session and explicitly load the preview:

```powershell
Import-Module Az -MinimumVersion 0.1.0 -MaximumVersion 0.1.9999
Get-Module -Name Az | Select-Object Name, Version
```

The loaded `Az` version should be in the `0.1.*` range. The preview installs side by side with classic Azure PowerShell. An unversioned `Import-Module Az` loads the highest installed version, which is classic Az when both are installed. Running a cmdlet before explicitly importing the preview can also autoload classic modules. To change versions, start a new session; see [Switch between classic and preview Azure PowerShell](./switch-azps-versions.md).

## Other install scenarios

| You want to… | Guide |
| --- | --- |
| Install a single module, pin a version, or install machine-wide | [install-powershell-modules.md](./install-powershell-modules.md) |
| Install the Azure CLI public preview | [install-overview.md](../../azure-cli/docs-conceptual/install-overview.md) |

## What's in a release

| Category | Files | Platforms | Notes |
| --- | --- | --- | --- |
| Module roll-up | `azps-cmdlets-<version>.tar.gz` | agnostic | Convenience bundle of all PowerShell module packages. |
| PowerShell module packages | `Az.*.nupkg` | agnostic | Install with the bundled `InstallModule.ps1`. Requires PowerShell 7.6+. |
