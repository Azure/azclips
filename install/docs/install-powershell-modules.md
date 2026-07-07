# Install azps (Az PowerShell Modules)

> This page covers **azps** (the Az PowerShell modules) — a separate product from azcli. For the CLI, see the [install guide](../README.md).

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

## Option A — install with the bundled script (recommended)

```bash
version="0.1.0"
tar -xzf "./azps-cmdlets-$version.tar.gz" -C azps-cmdlets
```

Then from PowerShell 7.6+:

```powershell
pwsh -File ./azps-cmdlets/InstallModule.ps1
```

This registers the extracted folder as a temporary local repository and installs the `Az` roll-up (and all `Az.*` dependencies) for the current user.

### Script options

| Parameter | Default | Purpose |
| --- | --- | --- |
| `-ModuleName` | `Az` | Install a single module instead of the roll-up, e.g. `-ModuleName Az.Compute`. |
| `-RequiredVersion` | inferred from `Az.<version>.nupkg` | Pin a specific version. |
| `-Scope` | `CurrentUser` | Use `AllUsers` for a machine-wide install (needs elevation). |
| `-RepositoryName` | `LocalAzclips` | Name of the temporary local PSRepository. |

Example — install just the Compute module for all users:

```powershell
pwsh -File ./azps-cmdlets/InstallModule.ps1 -ModuleName Az.Compute -Scope AllUsers
```

## Option B — install manually

If you prefer to run the steps yourself (PowerShell 7.6+):

```powershell
$version = '0.1.0'
$repoRoot = (Resolve-Path ./azps-cmdlets).Path

Register-PSRepository -Name LocalAzclips `
  -SourceLocation $repoRoot -PublishLocation $repoRoot `
  -InstallationPolicy Trusted -PackageManagementProvider NuGet

Install-Module -Name Az -RequiredVersion $version `
  -Repository LocalAzclips -Scope CurrentUser -Force -AllowClobber

Unregister-PSRepository -Name LocalAzclips
```

## Verify

```powershell
Import-Module Az
Get-Module Az.* -ListAvailable | Select-Object Name, Version
```

The `Az.*` modules should be listed. Cmdlets that contact Azure require sign-in (`Connect-AzAccount`) and a subscription — being prompted to authenticate means the install itself is working.

## Troubleshooting

| Symptom | Fix |
| --- | --- |
| Module fails to import / load error | Confirm PowerShell 7.6+ (`$PSVersionTable.PSVersion`). 7.5 and 5.1 cannot load these cmdlets. |
| `Register-PSRepository` says the repo already exists | Run `Unregister-PSRepository -Name LocalAzclips`, then retry (the bundled script does this automatically). |
| `Install-Module` cannot find the module | Run it from the extracted `azps-cmdlets` folder, or pass the correct `-Repository`/`SourceLocation` path. |
| Command conflicts with an existing Az install | Open a fresh PowerShell session, or use `-AllowClobber`. |
| Need a machine-wide install | Use `-Scope AllUsers` from an elevated PowerShell session. |
