# Scenario 8: Complete a task when a command is missing or fails

**User goal:** "I want to discover the right command, add a missing capability or use authenticated REST, and understand a failure without losing my work."

**Successful outcome:** The user can inspect a test resource through an available command or REST request, diagnose a controlled failure and restore test configuration and extension state.

## Prerequisites

- AzCLIPS CLI and/or the preview Az PowerShell modules, installed and signed in as described in [Scenario 1](https://github.com/Azure/azclips/blob/docs/scenario-2-feedback/docs/scenario-1-get-started-and-profiles.md). The modules require PowerShell 7.6+.
- For the optional extension step: a disposable CLI installation and a test extension package (`.nupkg`).
- For the optional AI troubleshooting step: AI troubleshooting access for your account. It is available from the CLI only.

## User journey

Choose one command path below: **AzCLIPS CLI in Bash**, **AzCLIPS CLI in PowerShell** or **AzCLIPS PowerShell**. Each path follows the same steps.

- **1. Discover commands:** Read the built-in help and create a small test resource group.
- **2. Use authenticated REST:** Read the test group through a direct REST request using your existing sign-in.
- **3. Understand a failure:** Request a group that does not exist and inspect the error.
- **4. Recover with AI troubleshooting (CLI only):** Start AI troubleshooting, select the failed command and review the explanation and suggested fix.
- **5. Add a capability (optional, CLI only):** Add a test extension and run one of its commands.
- **6. Clean up:** Delete the test group, remove the test extension and restore any logging change.

## AzCLIPS CLI in Bash

```bash
sub="<test-subscription-id>"
location="<region>"
run=$(date +%Y%m%d%H%M%S)
rg="azclips-recovery-$run"
uri="/subscriptions/$sub/resourceGroups/$rg?api-version=2021-04-01"
package="<path-to-test-extension.nupkg>"
extension="<test-extension-name>"
```

**1. Discover commands**

```bash
az --help
az group create --help
az group create --subscription-id "$sub" --name "$rg" --location "$location"
```

**2. Use authenticated REST**

```bash
az rest --method GET --uri "$uri"
az group show --subscription-id "$sub" --name "$rg"
```

**3. Understand a failure**

```bash
az group show --subscription-id "$sub" --name "$rg-missing"
echo "Exit code: $?"
```

Optional: record the current logging level, increase it and repeat the failing command.

```bash
az config get --key core.logging.level
az config set --key core.logging.level --value debug
az group show --subscription-id "$sub" --name "$rg-missing"
```

**4. Recover with AI troubleshooting**

```bash
az troubleshoot
```

Select the failed `az group show` command from the recent commands, review the explanation and suggested fix, and apply a suggestion only if it is appropriate. Use only non-sensitive commands.

**5. Add a capability (optional)**

```bash
az extension add --path "$package"
az extension list
az extension show --name "$extension"
```

Run one of the extension's commands and confirm it returns the expected result.

**6. Clean up**

```bash
az extension remove --name "$extension"
az group delete --subscription-id "$sub" --name "$rg"
az group exists --subscription-id "$sub" --name "$rg"
```

Restore logging: set `core.logging.level` back to the value you recorded with `az config set --key core.logging.level --value <recorded-value>`. If no value was set originally, run `az config delete --key core.logging.level`.

## AzCLIPS CLI in PowerShell

```powershell
$sub = '<test-subscription-id>'
$location = '<region>'
$run = Get-Date -Format 'yyyyMMddHHmmss'
$rg = "azclips-recovery-$run"
$uri = "/subscriptions/$sub/resourceGroups/${rg}?api-version=2021-04-01"
$package = '<path-to-test-extension.nupkg>'
$extension = '<test-extension-name>'
```

**1. Discover commands**

```powershell
az --help
az group create --help
az group create --subscription-id $sub --name $rg --location $location
```

**2. Use authenticated REST**

```powershell
az rest --method GET --uri $uri
az group show --subscription-id $sub --name $rg
```

**3. Understand a failure**

```powershell
az group show --subscription-id $sub --name "$rg-missing"
"Exit code: $LASTEXITCODE"
```

Optional: record the current logging level, increase it and repeat the failing command.

```powershell
az config get --key core.logging.level
az config set --key core.logging.level --value debug
az group show --subscription-id $sub --name "$rg-missing"
```

**4. Recover with AI troubleshooting**

```powershell
az troubleshoot
```

Select the failed `az group show` command from the recent commands, review the explanation and suggested fix, and apply a suggestion only if it is appropriate. Use only non-sensitive commands.

**5. Add a capability (optional)**

```powershell
az extension add --path $package
az extension list
az extension show --name $extension
```

Run one of the extension's commands and confirm it returns the expected result.

**6. Clean up**

```powershell
az extension remove --name $extension
az group delete --subscription-id $sub --name $rg
az group exists --subscription-id $sub --name $rg
```

Restore logging: set `core.logging.level` back to the value you recorded with `az config set --key core.logging.level --value <recorded-value>`. If no value was set originally, run `az config delete --key core.logging.level`.

## AzCLIPS PowerShell

CLI extensions and AI troubleshooting apply to the AzCLIPS CLI only, so steps 4 and 5 are not part of this path.

```powershell
$sub = '<test-subscription-id>'
$location = '<region>'
$run = Get-Date -Format 'yyyyMMddHHmmss'
$rg = "azclips-recovery-$run"
$uri = "/subscriptions/$sub/resourceGroups/${rg}?api-version=2021-04-01"
```

**1. Discover commands**

```powershell
Get-Help New-AzResourceGroup -Examples
New-AzResourceGroup -SubscriptionId $sub -Name $rg -Location $location
```

**2. Use authenticated REST**

```powershell
Invoke-AzRestMethod -Method GET -Uri $uri
Get-AzResourceGroup -SubscriptionId $sub -Name $rg
```

**3. Understand a failure**

```powershell
Get-AzResourceGroup -SubscriptionId $sub -Name "$rg-missing" -ErrorAction Stop
```

Optional: record the current logging level, increase it and repeat the failing command.

```powershell
Get-AzConfig -Key core.logging.level
Set-AzConfig -Key core.logging.level -Value debug
Get-AzResourceGroup -SubscriptionId $sub -Name "$rg-missing" -ErrorAction Stop
```

**6. Clean up**

```powershell
Remove-AzResourceGroup -SubscriptionId $sub -Name $rg -Force
Get-AzResourceGroup -SubscriptionId $sub -Name $rg -ErrorAction SilentlyContinue
```

Restore logging: set `core.logging.level` back to the value you recorded with `Set-AzConfig -Key core.logging.level -Value <recorded-value>`. If no value was set originally, run `Remove-AzConfig -Key core.logging.level`.
