# Scenario 1: Set up AzCLIPS and move between work environments

**User goal:** "I want to try AzCLIPS alongside my existing tools and switch between work environments without repeatedly rebuilding my sign-in context."

**Successful outcome:** Both preview surfaces run without replacing existing tools, and named profiles select the intended tenant and subscription.

## Prerequisites

- [GitHub CLI](https://cli.github.com/), signed in with `gh auth login` and with access to the [AzCLIPS releases](https://github.com/Azure/azclips/releases).
- [PowerShell 7.6+](https://aka.ms/powershell) for the AzCLIPS PowerShell modules and for the Windows CLI path.
- Two Azure subscriptions or accounts you can sign in to. They can belong to the same tenant.

Find each subscription ID and its directory (tenant) ID in **Azure portal > Subscriptions > select the subscription > Overview**. Use the same tenant ID twice if both subscriptions are in one directory.

## User journey

Choose one command path below: **AzCLIPS CLI in Bash** (Linux and macOS), **AzCLIPS CLI in PowerShell** (Windows) or **AzCLIPS PowerShell**. Each path follows the same steps. Installing the preview does not replace your existing Azure CLI or Azure PowerShell.

- **1. Install the preview:** Install the preview next to your existing tools and make it active for the current session.
- **2. Verify the installation:** Confirm the preview starts and is the one your session uses.
- **3. Save two sign-in contexts:** Sign in with a browser for one context and with a device code for another, saving each as a named profile.
- **4. Switch work environments:** List and inspect the profiles, switch between them and confirm the resources shown match the selected subscription.
- **5. Refresh and retrieve a token (optional):** Re-authenticate a saved profile and check that an access token can be retrieved.
- **6. Clean up:** Sign out of the test profiles, remove them and restore the profile that was active before.

Before step 3, note your active profile with `az profile show --active` or `Get-AzProfile -Active`, and do not overwrite existing profiles.

## AzCLIPS CLI in Bash

```bash
set -euo pipefail
tenant_a="<tenant-A-id>"
sub_a="<subscription-A-id>"
tenant_b="<tenant-B-id>"
sub_b="<subscription-B-id>"
run=$(date +%Y%m%d%H%M%S)
profile_a="review-a-$run"
profile_b="review-b-$run"
```

**1. Install the preview**

```bash
gh api repos/Azure/azclips/contents/install/azcli-install.sh -H 'Accept: application/vnd.github.raw' > azcli-install.sh
bash azcli-install.sh
export PATH="$HOME/.local/share/azcli:$PATH"
```

**2. Verify the installation**

```bash
command -v az
az --version
az --help
```

**3. Save two sign-in contexts**

```bash
az login --tenant-id "$tenant_a" --subscription-id "$sub_a" --out-profile "$profile_a"
az login --use-device-code --tenant-id "$tenant_b" --subscription-id "$sub_b" --out-profile "$profile_b"
```

**4. Switch work environments**

```bash
az profile list
az profile show "$profile_a"
az profile set --name "$profile_a"
az group list
az group list --profile "$profile_b"
az profile show --active
```

**5. Refresh and retrieve a token (optional)**

```bash
az login --profile "$profile_a"
az account get-access-token --resource-type arm
```

**6. Clean up**

```bash
for p in "$profile_a" "$profile_b"; do
  az profile set --name "$p"
  az logout
  az profile remove --name "$p"
done
```

Restore your previous profile with `az profile set --name <previous-profile>`.

## AzCLIPS CLI in PowerShell

```powershell
$ErrorActionPreference = 'Stop'
$tenantA = '<tenant-A-id>'
$subA = '<subscription-A-id>'
$tenantB = '<tenant-B-id>'
$subB = '<subscription-B-id>'
$run = Get-Date -Format 'yyyyMMddHHmmss'
$profileA = "review-a-$run"
$profileB = "review-b-$run"
```

**1. Install the preview**

```powershell
gh api repos/Azure/azclips/contents/install/azcli-install.ps1 -H 'Accept: application/vnd.github.raw' > azcli-install.ps1
.\azcli-install.ps1
$env:PATH = "$env:LOCALAPPDATA\Programs\azcli;$env:PATH"
```

**2. Verify the installation**

```powershell
Get-Command az
az --version
az --help
```

**3. Save two sign-in contexts**

```powershell
az login --tenant-id $tenantA --subscription-id $subA --out-profile $profileA
az login --use-device-code --tenant-id $tenantB --subscription-id $subB --out-profile $profileB
```

**4. Switch work environments**

```powershell
az profile list
az profile show $profileA
az profile set --name $profileA
az group list
az group list --profile $profileB
az profile show --active
```

**5. Refresh and retrieve a token (optional)**

```powershell
az login --profile $profileA
az account get-access-token --resource-type arm
```

**6. Clean up**

```powershell
foreach ($p in $profileA, $profileB) {
    az profile set --name $p
    az logout
    az profile remove --name $p
}
```

Restore your previous profile with `az profile set --name <previous-profile>`.

## AzCLIPS PowerShell

Run this path in a fresh PowerShell 7.6+ session with no classic Az modules loaded.

```powershell
$ErrorActionPreference = 'Stop'
$tenantA = '<tenant-A-id>'
$subA = '<subscription-A-id>'
$tenantB = '<tenant-B-id>'
$subB = '<subscription-B-id>'
$run = Get-Date -Format 'yyyyMMddHHmmss'
$profileA = "review-a-$run"
$profileB = "review-b-$run"
```

**1. Install the preview**

Download the module bundle into a new temporary folder. The bundled installer installs the modules without replacing your existing ones.

```powershell
$stage = Join-Path ([IO.Path]::GetTempPath()) "azclips-modules-$run"
New-Item -ItemType Directory -Path $stage | Out-Null
gh release download --repo Azure/azclips --pattern 'azps-cmdlets-*.tar.gz' --dir $stage
$bundle = Get-ChildItem -LiteralPath $stage -Filter 'azps-cmdlets-*.tar.gz'
tar -xzf $bundle.FullName -C $stage
& (Join-Path $stage 'InstallModule.ps1')
$rollup = Get-ChildItem -LiteralPath $stage -Filter 'Az.*.nupkg' | Where-Object { $_.BaseName -match '^Az\.\d' }
$previewVersion = $rollup.BaseName.Substring(3)
```

**2. Verify the installation**

```powershell
Import-Module Az.Accounts -RequiredVersion $previewVersion
Import-Module Az.Resources -RequiredVersion $previewVersion
Get-Command Connect-AzAccount, Get-AzResourceGroup
Get-Module Az.Accounts, Az.Resources | Select-Object Name, Version, Path
```

**3. Save two sign-in contexts**

```powershell
Connect-AzAccount -TenantId $tenantA -SubscriptionId $subA -OutProfile $profileA
Connect-AzAccount -UseDeviceCode -TenantId $tenantB -SubscriptionId $subB -OutProfile $profileB
```

**4. Switch work environments**

```powershell
Get-AzProfile
Get-AzProfile -Name $profileA
Set-AzProfile -Name $profileA
Get-AzResourceGroup
Set-AzProfile -Name $profileB
Get-AzResourceGroup
Get-AzProfile -Active
```

**5. Refresh and retrieve a token (optional)**

```powershell
Connect-AzAccount -Profile $profileA
Get-AzAccessToken
```

**6. Clean up**

```powershell
foreach ($p in $profileA, $profileB) {
    Set-AzProfile -Name $p
    Disconnect-AzAccount
    Remove-AzProfile -Name $p
}
```

Restore your previous profile with `Set-AzProfile -Name <previous-profile>`.
