# Scenario 1: Set up AzCLIPS and move between work environments

**User goal:** "I want to install AzCLIPS next to my existing Azure CLI and Azure PowerShell, make sure the extensions I need are available, switch between the classic and preview tools when I need to, and use saved profiles to move between my tenants and subscriptions in the preview."

**Successful outcome:** The user can confirm the preview and its extensions are installed, switch between the classic and preview tools, and use named profiles in the preview to select the intended tenant and subscription. Signing in to one tool does not sign in the other.

## Prerequisites

- [GitHub CLI](https://cli.github.com/), signed in with `gh auth login` and with access to the [AzCLIPS releases](https://github.com/Azure/azclips/releases).
- [PowerShell 7.6+](https://aka.ms/powershell) for the AzCLIPS PowerShell modules and for the Windows CLI path.
- Your existing (classic) Azure CLI and/or Azure PowerShell, already installed. Step 4 switches between them and the preview.
- Two Azure subscriptions or accounts you can sign in to. They can belong to the same tenant.

Find each subscription ID and its directory (tenant) ID in **Azure portal > Subscriptions > select the subscription > Overview**. Use the same tenant ID twice if both subscriptions are in one directory.

## User journey

Choose one command path below: **AzCLIPS CLI in Bash** (Linux and macOS), **AzCLIPS CLI in PowerShell** (Windows) or **AzCLIPS PowerShell**. Each path follows the same steps. Installing the preview does not replace your existing Azure CLI or Azure PowerShell. The preview does not share sign-in context or tokens with them, so profiles switch work environments within the preview only.

- **1. Install the preview:** Install the preview next to your existing tools and make it active for the current session.
- **2. Verify the installation:** Confirm the preview starts and is the one your session uses.
- **3. Check your extensions (optional, CLI only):** List the installed extensions and confirm `compute` is present. If it is missing, add it from the release's extensions bundle.
- **4. Switch between classic and preview:** Confirm which tool is active, switch to the classic tool and switch back.
- **5. Save two sign-in contexts:** Sign in with a browser for one context and with a device code for another, saving each as a named profile.
- **6. Switch between profiles:** List and inspect the profiles, switch between them and confirm the resources shown match the selected subscription.
- **7. Refresh and retrieve a token (optional):** Re-authenticate a saved profile and check that an access token can be retrieved.
- **8. Clean up:** Sign out of the test profiles, remove them, remove the extension only if you added it, and restore the tool and profile that were active before.

Before step 5, note your active profile with `az profile show --active` or `Get-AzProfile -Active`, and do not overwrite existing profiles.

## AzCLIPS CLI in Bash

```bash
set -euo pipefail
tenant_a="<tenant-A-id>"
sub_a="<subscription-A-id>"
tenant_b="<tenant-B-id>"
sub_b="<subscription-B-id>"
# Unique suffix so this run's resources don't collide
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

**3. Check your extensions (optional)**

A full installation includes the `compute` extension. Confirm it is installed:

```bash
az extension list
az extension show --name compute
```

If `compute` is not listed, download the extensions bundle from the release you installed, unpack it and add the extension:

```bash
gh release download --repo Azure/azclips --pattern 'azcli-extensions-*.tar.gz' --dir . --clobber
mkdir -p azcli-extensions
tar -xzf ./azcli-extensions-*.tar.gz -C azcli-extensions
az extension add --path ./azcli-extensions/azcli.extensions.compute.*.nupkg
az extension list
```

**4. Switch between classic and preview**

Define a helper that puts the preview on and off your `PATH` for this shell only. Switch to the classic Azure CLI and back.

```bash
switch-az() {
  local preview_dir="$HOME/.local/share/azcli"
  if [[ "$(command -v az)" == "$preview_dir/az" ]]; then
    export PATH="${PATH#$preview_dir:}"
  else
    export PATH="$preview_dir:$PATH"
  fi
  command -v az
}
switch-az
az --version
switch-az
az --version
```

The first `switch-az` makes the classic `az` active. The second makes the preview active again.

**5. Save two sign-in contexts**

```bash
az login --tenant-id "$tenant_a" --subscription-id "$sub_a" --out-profile "$profile_a"
az login --use-device-code --tenant-id "$tenant_b" --subscription-id "$sub_b" --out-profile "$profile_b"
```

**6. Switch between profiles**

```bash
az profile list
az profile show "$profile_a"
az profile set --name "$profile_a"
az group list
az group list --profile "$profile_b"
az profile show --active
```

**7. Refresh and retrieve a token (optional)**

```bash
az login --profile "$profile_a"
az account get-access-token --resource-type arm
```

**8. Clean up**

```bash
for p in "$profile_a" "$profile_b"; do
  az profile set --name "$p"
  az logout
  az profile remove --name "$p"
done
```

Restore your previous profile with `az profile set --name <previous-profile>`. If you added `compute` in step 3, remove it with `az extension remove --name compute`. Close the shell, or run `switch-az`, to return to the tool that was active when you started.

## AzCLIPS CLI in PowerShell

```powershell
$ErrorActionPreference = 'Stop'
$tenantA = '<tenant-A-id>'
$subA = '<subscription-A-id>'
$tenantB = '<tenant-B-id>'
$subB = '<subscription-B-id>'
# Unique suffix so this run's resources don't collide
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

**3. Check your extensions (optional)**

A full installation includes the `compute` extension. Confirm it is installed:

```powershell
az extension list
az extension show --name compute
```

If `compute` is not listed, download the extensions bundle from the release you installed, unpack it and add the extension:

```powershell
gh release download --repo Azure/azclips --pattern 'azcli-extensions-*.tar.gz' --dir . --clobber
New-Item -ItemType Directory -Force -Path azcli-extensions | Out-Null
$bundle = Get-ChildItem azcli-extensions-*.tar.gz | Sort-Object LastWriteTime -Descending | Select-Object -First 1
tar -xzf $bundle.FullName -C azcli-extensions
$compute = Get-ChildItem azcli-extensions/azcli.extensions.compute.*.nupkg | Select-Object -First 1
az extension add --path $compute.FullName
az extension list
```

**4. Switch between classic and preview**

Define a helper that puts the preview on and off your `PATH` for this session only. Switch to the classic Azure CLI and back.

```powershell
function Switch-Az {
    $previewDir = Join-Path $env:LOCALAPPDATA 'Programs\azcli'
    $sep = [IO.Path]::PathSeparator
    $current = Get-Command -Name az -CommandType Application -ErrorAction SilentlyContinue |
        Select-Object -First 1 -ExpandProperty Source
    if ($current -and (Split-Path -Parent $current) -eq $previewDir) {
        $env:PATH = ($env:PATH -split [regex]::Escape($sep) | Where-Object { $_ -and $_ -ne $previewDir }) -join $sep
    }
    else {
        $env:PATH = "$previewDir$sep$env:PATH"
    }
    Get-Command -Name az -CommandType Application | Select-Object -First 1 -ExpandProperty Source
}
Switch-Az
az --version
Switch-Az
az --version
```

The first `Switch-Az` makes the classic `az` active. The second makes the preview active again.

**5. Save two sign-in contexts**

```powershell
az login --tenant-id $tenantA --subscription-id $subA --out-profile $profileA
az login --use-device-code --tenant-id $tenantB --subscription-id $subB --out-profile $profileB
```

**6. Switch between profiles**

```powershell
az profile list
az profile show $profileA
az profile set --name $profileA
az group list
az group list --profile $profileB
az profile show --active
```

**7. Refresh and retrieve a token (optional)**

```powershell
az login --profile $profileA
az account get-access-token --resource-type arm
```

**8. Clean up**

```powershell
foreach ($p in $profileA, $profileB) {
    az profile set --name $p
    az logout
    az profile remove --name $p
}
```

Restore your previous profile with `az profile set --name <previous-profile>`. If you added `compute` in step 3, remove it with `az extension remove --name compute`. Close the session, or run `Switch-Az`, to return to the tool that was active when you started.

## AzCLIPS PowerShell

Run this path in a fresh PowerShell 7.6+ session with no classic Az modules loaded. Step 3 is not part of this path, because CLI extensions apply to the AzCLIPS CLI only.

```powershell
$ErrorActionPreference = 'Stop'
$tenantA = '<tenant-A-id>'
$subA = '<subscription-A-id>'
$tenantB = '<tenant-B-id>'
$subB = '<subscription-B-id>'
# Unique suffix so this run's resources don't collide
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

**4. Switch between classic and preview**

A PowerShell session can load only one version of the Az modules, so you switch by starting a new session. Start a session that loads the classic modules and check their version.

```powershell
pwsh -NoExit -Command 'Import-Module Az; Get-Module Az | Select-Object Name, Version'
```

A higher version, such as 15.x, means the classic modules are active. Run `exit` to close that session. In your original session, confirm the preview is still loaded:

```powershell
Get-Module Az.Accounts | Select-Object Name, Version
```

A `0.1.*` version means the preview is active.

**5. Save two sign-in contexts**

```powershell
Connect-AzAccount -TenantId $tenantA -SubscriptionId $subA -OutProfile $profileA
Connect-AzAccount -UseDeviceCode -TenantId $tenantB -SubscriptionId $subB -OutProfile $profileB
```

**6. Switch between profiles**

```powershell
Get-AzProfile
Get-AzProfile -Name $profileA
Set-AzProfile -Name $profileA
Get-AzResourceGroup
Set-AzProfile -Name $profileB
Get-AzResourceGroup
Get-AzProfile -Active
```

**7. Refresh and retrieve a token (optional)**

```powershell
Connect-AzAccount -Profile $profileA
Get-AzAccessToken
```

**8. Clean up**

```powershell
foreach ($p in $profileA, $profileB) {
    Set-AzProfile -Name $p
    Disconnect-AzAccount
    Remove-AzProfile -Name $p
}
```

Restore your previous profile with `Set-AzProfile -Name <previous-profile>`. Close the session to return to the tool that was active when you started.
