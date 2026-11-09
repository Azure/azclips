---
title: Install the Next-Generation Azure PowerShell public preview
description: Learn how to install and explicitly select the Next-Generation Azure PowerShell public preview modules.
---

# Install the Next-Generation Azure PowerShell public preview

The Next-Generation Azure PowerShell public preview ships as **azps** — the Az PowerShell modules (`Az.*`). The modules require PowerShell 7.6 or later.

> [!NOTE]
> For the `az` command-line tool, see the [Next-Generation Azure CLI public preview install overview](../../azure-cli/docs-conceptual/install-overview.md).

## Install azps (one command)

Download the module bundle from [GitHub Releases](https://github.com/Azure/azclips/releases)
using your authenticated GitHub CLI (`gh`) session.

> [!IMPORTANT]
> These steps require your GitHub account to have access to the repository and release assets.
> A GitHub sign-in alone doesn't grant repository access. If the repository or release isn't visible
> to your account, these download commands won't work.

**Prerequisites:**

- [PowerShell 7.6+](https://aka.ms/powershell)
- [GitHub CLI](https://cli.github.com/) signed in (`gh auth login`)

**Download and install:**

```powershell
gh release download --repo Azure/azclips --pattern 'azps-cmdlets-*.tar.gz' --dir . --clobber
New-Item -ItemType Directory -Force -Path azps-cmdlets | Out-Null
$bundle = Get-ChildItem azps-cmdlets-*.tar.gz | Sort-Object LastWriteTime -Descending | Select-Object -First 1
tar -xzf $bundle.FullName -C azps-cmdlets
./azps-cmdlets/InstallModule.ps1
```

## Verify

Start a fresh PowerShell 7.6 or later session. Replace `<preview-module-version>` with the `Az`
version reported by `InstallModule.ps1`, not a classic Az version:

```powershell
Get-Module -Name Az -ListAvailable | Select-Object Name, Version, ModuleBase
Import-Module -Name Az -RequiredVersion <preview-module-version>
Get-Module -Name Az, Az.Accounts | Select-Object Name, Version, ModuleBase
```

Classic and preview modules share module and cmdlet names. An unversioned `Import-Module Az`
can select classic modules. Choose a version before running any Az cmdlet, and start a new
PowerShell process to switch. See [switch between classic and preview modules](./switch-azps-versions.md).

## Sign in and try a command

Continue with [get started with Next-Generation Azure PowerShell](./getting-started.md) to sign in
to Azure, check your active profile, and run a read-only command. GitHub authentication used to
download the bundle doesn't sign you in to Azure.

## Other install scenarios

| You want to… | Guide |
| --- | --- |
| Install a single module, pin a version, or install machine-wide | [install-powershell-modules.md](./install-powershell-modules.md) |
| Install the Next-Generation Azure CLI public preview | [install-overview.md](../../azure-cli/docs-conceptual/install-overview.md) |

## What's in a release

| Category | Files | Platforms | Notes |
| --- | --- | --- | --- |
| Module roll-up | `azps-cmdlets-<version>.tar.gz` | agnostic | Convenience bundle of all PowerShell module packages. |
| PowerShell module packages | `Az.*.nupkg` | agnostic | Install with the bundled `InstallModule.ps1`. Requires PowerShell 7.6+. |
