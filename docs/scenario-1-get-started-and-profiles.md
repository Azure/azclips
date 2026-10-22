# Scenario 1: Set up AzCLIPS and move between work environments

**Customer goal:** "I want to try AzCLIPS alongside my existing tools and switch between work environments without repeatedly rebuilding my sign-in context."

**Successful outcome:** Both preview surfaces run without replacing existing tools, and named profiles select the intended tenant and subscription.

## Prerequisites

- [PowerShell 7.6+](https://aka.ms/powershell) and [GitHub CLI](https://cli.github.com/), signed in with `gh auth login` and access to the AzCLIPS releases.
- Two Azure subscriptions or accounts you can sign in to. They can belong to the same tenant; no separate approval process is implied.

## Shared inputs

Commands use PowerShell syntax. Use a fresh session with no classic Az modules loaded. Stop on errors; check `$LASTEXITCODE` after native commands.

Get each subscription ID and its directory (tenant) ID from **Azure portal > Subscriptions > select the subscription > Overview**. Replace the four placeholders below. Use the same tenant ID twice if both subscriptions are in one directory. Installation steps below download the packages and determine the module version; no archive path or version lookup is needed.

```powershell
$ErrorActionPreference = 'Stop'
$az = "$env:LOCALAPPDATA\Programs\azcli\az.exe"
$tenantA = '<tenant-A-id>'
$subA = '<subscription-A-id>'
$tenantB = '<tenant-B-id>'
$subB = '<subscription-B-id>'
$run = Get-Date -Format 'yyyyMMddHHmmss'
$profileA = "review-a-$run"
$profileB = "review-b-$run"
```

## Install the preview

These commands use the latest available [AzCLIPS release](https://github.com/Azure/azclips/releases). Record its tag for feedback. For a coordinated test run, everyone should use the same release tag; add `--tag <release-tag>` to the PowerShell bundle download, and use that release's CLI archive via the [manual installation guide](https://github.com/Azure/azclips/blob/docs/scenario-2-feedback/docs/azure-cli/docs-conceptual/install-cli-manual.md).

### AzCLIPS CLI on Windows

Download and run the repository's installer. It selects the package for your machine and does not change persistent PATH.

```powershell
gh api repos/Azure/azclips/contents/install/azcli-install.ps1 -H 'Accept: application/vnd.github.raw' > azcli-install.ps1
if ($LASTEXITCODE -ne 0) { throw 'CLI installer download failed.' }
.\azcli-install.ps1
```

### AzCLIPS PowerShell

Download the module bundle into a new temporary folder. The bundled installer reads the version from the package; PowerShell Gallery is not a PP installation option.

```powershell
$stage = Join-Path $env:TEMP "azclips-modules-$run"
New-Item -ItemType Directory -Path $stage | Out-Null
gh release download --repo Azure/azclips --pattern 'azps-cmdlets-*.tar.gz' --dir $stage
if ($LASTEXITCODE -ne 0) { throw 'PowerShell bundle download failed.' }
$bundle = Get-ChildItem -LiteralPath $stage -Filter 'azps-cmdlets-*.tar.gz'
tar -xzf $bundle.FullName -C $stage
if ($LASTEXITCODE -ne 0) { throw 'PowerShell bundle extraction failed.' }
& (Join-Path $stage 'InstallModule.ps1')
$rollup = Get-ChildItem -LiteralPath $stage -Filter 'Az.*.nupkg' |
    Where-Object { $_.BaseName -match '^Az\.\d' }
$previewVersion = $rollup.BaseName.Substring(3)
```

Import the exact bundle version in step 1 to avoid accidentally loading classic Az modules. Further installation details: [CLI guide](https://github.com/Azure/azclips/blob/docs/scenario-2-feedback/docs/azure-cli/docs-conceptual/install-overview.md) and [PowerShell bundle guide](https://github.com/Azure/azclips/blob/docs/scenario-2-feedback/docs/azure-powershell/docs-conceptual/install-powershell-modules.md).

## Customer journey

CLI and PowerShell sign-in commands are alternative paths to the same named profiles. Record the previously active profile before changing it. Do not overwrite existing profiles.

| Step | Customer action | Commands (AzCLIPS CLI & AzCLIPS PowerShell) | Expected result / verification |
|---|---|---|---|
| **1. Install alongside existing tools** | Follow the installation blocks above, then verify both preview surfaces. | **AzCLIPS CLI:**<br>`& $az --version`<br>`& $az --help`<br><br>**AzCLIPS PowerShell:**<br>`Import-Module Az.Accounts -RequiredVersion $previewVersion`<br>`Import-Module Az.Resources -RequiredVersion $previewVersion`<br>`Get-Command Connect-AzAccount, Get-AzResourceGroup`<br>`Get-Module Az.Accounts, Az.Resources \| Select-Object Name, Version, Path` | Preview CLI starts at the explicit path. Cmdlets resolve to the bundle's preview versions and paths. Classic tools remain installed and persistent PATH is unchanged. Use separate fresh processes for classic and preview PowerShell. |
| **2. Save two sign-in contexts** | Sign in with a browser for one context and a device code for another. | **AzCLIPS CLI:**<br>`& $az login --tenant-id $tenantA --subscription-id $subA --out-profile $profileA`<br>`& $az login --use-device-code --tenant-id $tenantB --subscription-id $subB --out-profile $profileB`<br><br>**AzCLIPS PowerShell:**<br>`Connect-AzAccount -TenantId $tenantA -SubscriptionId $subA -OutProfile $profileA`<br>`Connect-AzAccount -UseDeviceCode -TenantId $tenantB -SubscriptionId $subB -OutProfile $profileB` | Two named profiles retain the intended context. The most recently authenticated profile is active. Do not retain device codes in evidence. |
| **3. Switch work environments** | List, inspect and switch profiles, then inspect resources in the selected environment. | **AzCLIPS CLI:**<br>`& $az profile list`<br>`& $az profile show $profileA`<br>`& $az profile set --name $profileA`<br>`& $az group list`<br>`& $az group list --profile $profileB`<br>`& $az profile show --active`<br><br>**AzCLIPS PowerShell:**<br>`Get-AzProfile`<br>`Get-AzProfile -Name $profileA`<br>`Set-AzProfile -Name $profileA`<br>`Get-AzResourceGroup`<br>`Set-AzProfile -Name $profileB`<br>`Get-AzResourceGroup`<br>`Get-AzProfile -Active` | Resource results match the selected subscription, including a valid empty list. The CLI per-command profile override does not change the active profile. Verify context, not just resource names. |
| **4. Refresh context and retrieve a token** | Re-authenticate a saved profile and inspect token metadata without displaying the token. | **AzCLIPS CLI:**<br>`& $az login --profile $profileA`<br>`& $az account get-access-token --resource-type arm`<br><br>**AzCLIPS PowerShell:**<br>`Connect-AzAccount -Profile $profileA`<br>`Get-AzAccessToken` | Re-authentication preserves the profile's identity and context. CLI token output is redacted by default; PowerShell token is a SecureString. Do not opt into plaintext or log tokens. |
| **5. End the trial session** | Sign out of the test profiles, remove them and restore the previous active profile if one existed. | **AzCLIPS CLI, for each test profile:**<br>`& $az profile set --name $profileA`<br>`& $az logout`<br>`& $az profile remove --name $profileA`<br>Repeat for `$profileB`.<br><br>**AzCLIPS PowerShell, for each test profile:**<br>`Set-AzProfile -Name $profileA`<br>`Disconnect-AzAccount`<br>`Remove-AzProfile -Name $profileA`<br>Repeat for `$profileB`.<br><br>Restore the previously recorded profile with `profile set` / `Set-AzProfile`. | Test profiles are absent. No Azure resources were created. Keep the preview installation for the remaining scenarios; run classic PowerShell only in a separate fresh process. Sign-out can affect other profiles using the same cached account, so use test accounts. |

## Platform variants

- Repeat installation/startup on Windows x64/Arm64, Linux glibc/musl x64/Arm64 and macOS Intel/Apple Silicon with the matching release archive.
- For Linux/macOS, extract the CLI tarball into a separate test folder, run its `az` by full path and update `$az` accordingly. Windows archive commands above do not apply.
- Repeat with full and lean packages; exercise add-on installation through scenario 8.
