# Scenario 8: Complete a task when a command is missing or fails

**October 21 scope note:** REST and CLI extension management are reported completed, subject to build/source verification. Improved help is completed; Learn integration remains Pending. Live resource-name/Azure-aware completion is excluded from PP; skip that variant. AI command naming remains undecided, so `az troubleshoot` below is provisional.

**Customer goal:** "I want to discover the right command, add a missing capability or use authenticated REST, and understand a failure without losing my work."

**Successful outcome:** The customer can inspect a test resource through an available command or approved REST request, diagnose a controlled failure and restore test configuration and extension state.

## Prerequisites

- AzCLIPS CLI and preview PowerShell modules installed and authenticated in PowerShell 7.6+.
- An approved add-on package and feed with two test versions for extension-management variants; use a disposable CLI test installation and extension registry.
- Approved AI access for the optional troubleshooting variant.

## Shared inputs

Commands use PowerShell syntax. Stop on unexpected errors; check `$LASTEXITCODE` after CLI commands. **Pending** means Engineering confirmation is needed, not that execution is merely outstanding.

```powershell
$ErrorActionPreference = 'Stop'
$az = '<full-path-to-disposable-AzCLIPS-executable>'
$sub = '<test-subscription-id>'
$location = '<approved-region>'
$run = Get-Date -Format 'yyyyMMddHHmmss'
$rg = "azclips-recovery-$run"
$package = '<full-path-to-approved-add-on.nupkg>'
$extension = '<package-ID-from-the-approved-package>'
$feed = '<approved-feed-URL>'
$nextVersion = '<approved-newer-test-version>'
$uri = "/subscriptions/$sub/resourceGroups/${rg}?api-version=2021-04-01"
```

## Customer journey

CLI and PowerShell commands are alternatives except for CLI-only features. Use only non-sensitive test commands in troubleshooting.

| Step | Customer action | Commands (AzCLIPS CLI & AzCLIPS PowerShell) | Expected result / verification |
|---|---|---|---|
| **1. Discover commands and prepare a resource** | Read terminal help, inspect config and create a small test resource. | **AzCLIPS CLI:**<br>`& $az --help`<br>`& $az group create --help`<br>`& $az config list`<br>`& $az config get --key core.logging.level`<br>`& $az group create --subscription-id $sub --name $rg --location $location`<br><br>**AzCLIPS PowerShell:**<br>`Get-Help New-AzResourceGroup -Examples`<br>`Get-AzConfigList`<br>`Get-AzConfig -Key core.logging.level`<br>`New-AzResourceGroup -SubscriptionId $sub -Name $rg -Location $location`<br><br>**Pending:** Microsoft Learn reference links; Azure-aware and live resource-name completion. | Help describes the preview command and examples. Record any existing user override for logging before changing it. Group exists in the intended context. |
| **2. Add a missing command** | Install, inspect, pin, update and unpin an add-on in the disposable CLI environment. | **Pending release confirmation, source-backed AzCLIPS CLI commands:**<br>`& $az extension add --path $package`<br>`& $az extension list`<br>`& $az extension show --name $extension`<br>`& $az extension pin --name $extension`<br>`& $az extension update --name $extension --version $nextVersion --feed-url $feed`<br>`& $az extension unpin --name $extension`<br>Repeat update after unpinning and run the package's documented non-mutating command.<br><br>**AzCLIPS PowerShell:** CLI extension management is not a PowerShell cmdlet feature; verify the separately installed preview module with `Get-Command`. | Add-on commands become discoverable. Pin prevents an unforced update; after unpinning, update selects the approved newer version. Verify installed version and command behavior, not just package presence. Never modify a shared extension registry. |
| **3. Use authenticated REST** | Inspect the test group through REST to exercise the fallback when a dedicated resource command is unavailable. | **Pending release confirmation, AzCLIPS CLI:**<br>`& $az rest --method GET --uri $uri`<br><br>**Pending release confirmation, AzCLIPS PowerShell:**<br>`Invoke-AzRestMethod -Method GET -Uri $uri`<br><br>Compare with:<br>`& $az group show --subscription-id $sub --name $rg`<br>`Get-AzResourceGroup -SubscriptionId $sub -Name $rg`<br><br>**Pending variant:** Engineering supplies an approved ARM resource without a dedicated extension, its API version and any required payload; repeat the REST request for that resource. | REST returns a successful status and the same group ID/location as the dedicated command, using existing sign-in context. No manual bearer token is required. Caller still chooses the correct API version, URI and payload; this is not proof that every endpoint is supported. |
| **4. Diagnose a controlled failure** | Request a nonexistent group; inspect the error and optionally ask for AI help. | **AzCLIPS CLI:**<br>`& $az group show --subscription-id $sub --name "$rg-missing"`<br>Record `$LASTEXITCODE` immediately.<br><br>**AzCLIPS PowerShell:**<br>`Get-AzResourceGroup -SubscriptionId $sub -Name "$rg-missing" -ErrorAction Stop`<br><br>**Pending, CLI troubleshooting:**<br>`& $az troubleshoot`<br>Select the recent non-sensitive failing command and review the proposed explanation.<br><br>**Configuration variant, choose one:**<br>`& $az config set --key core.logging.level --value debug`<br>`Set-AzConfig -Key core.logging.level -Value debug`<br>Repeat the read and restore logging afterward.<br><br>**Pending REST negative variant:** request a nonexistent group through the step 3 REST commands. | Expected not-found produces a clear error; CLI reports failure and PowerShell exposes an error. REST preserves HTTP status/body and signals failure. AI picker selects the intended command; guidance is reviewed before any suggested action. PowerShell AI diagnosis is not part of this scenario. |
| **5. Shape results, give feedback and clean up** | View results in useful formats, open feedback and remove test changes. | **AzCLIPS CLI:**<br>`& $az group list --subscription-id $sub --query '[].name' --output tsv`<br>`& $az group show --subscription-id $sub --name $rg --output json`<br>Repeat show with `jsonc`, `table`, `tsv`, `yaml` and `yamlc`.<br>`& $az survey`<br>**Pending release confirmation:**<br>`& $az extension remove --name $extension`<br>`& $az extension list`<br><br>**AzCLIPS PowerShell:**<br>`Get-AzResourceGroup -SubscriptionId $sub -Name $rg`<br>`Get-AzResourceGroup -SubscriptionId $sub -Name $rg \| Select-Object Name, Location`<br>`Open-AzSurvey`<br><br>Delete the group using `& $az group delete --subscription-id $sub --name $rg` or `Remove-AzResourceGroup -SubscriptionId $sub -Name $rg -Force`.<br>Verify absence with CLI `group exists` or PowerShell lookup. | Query and formats return the intended results; PowerShell emits usable objects. Feedback page opens; submission is optional. Test group/add-on are absent. Restore the recorded logging override, or delete the override if none originally existed. Retain no sensitive debug output. |

## Session-only completion and configuration variants

- In a disposable shell, put the preview CLI directory first in session PATH and confirm `Get-Command az` resolves to it. Source the packaged `completions\az.completions.ps1`, then try `az group <Tab>` and `az group create --<Tab>`. Close that shell to discard PATH/completion changes; do not modify the persistent PowerShell profile.
- Verify native PowerShell cmdlet/parameter completion separately from CLI completion. Live resource-name completion remains **Pending**.
- Restore logging with `& $az config set --key core.logging.level --value <recorded-value>` or `Set-AzConfig -Key core.logging.level -Value <recorded-value>`.
- If the key originally had no user override, restore that state with `& $az config delete --key core.logging.level` or `Remove-AzConfig -Key core.logging.level`.
