---
title: Install the Azure PowerShell private preview
description: Learn how to install the Azure PowerShell private preview modules.
---

# Install the Azure PowerShell private preview

The Azure PowerShell private preview ships as **azps** — the Az PowerShell modules (`Az.*`). The modules require PowerShell 7.6 or later.

> [!NOTE]
> For the Azure CLI (`az` command-line tool), see the [Azure CLI private preview install overview](../../azure-cli/docs-conceptual/install-overview.md).

## Install azps (one command)

Private-preview artifacts live in a **private** GitHub repo; your account has **read** access. The installer uses your authenticated GitHub CLI (`gh`) session.

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

```powershell
Import-Module Az
Get-Module Az.* -ListAvailable | Select-Object Name, Version
```

## Other install scenarios

| You want to… | Guide |
| --- | --- |
| Install a single module, pin a version, or install machine-wide | [install-powershell-modules.md](./install-powershell-modules.md) |
| Install the Azure CLI private preview | [install-overview.md](../../azure-cli/docs-conceptual/install-overview.md) |
