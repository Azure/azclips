# Scenario 3: Give an application secure access to Azure resources

**October 21 scope note:** role-definition discovery is excluded from PP; use the supplied role ID and skip that Pending variant below. Generic resource inspection remains Pending discussion.

**Customer goal:** "I want my application to read a Key Vault secret using a managed identity instead of an embedded password."

**Successful outcome:** The workload reads a test secret using its assigned identity, loses access after the grant is revoked, and leaves no test identity or active test secret behind.

## Prerequisites

- AzCLIPS CLI and preview PowerShell modules installed and authenticated in PowerShell 7.6+.
- An approved Azure test host that supports user-assigned identity attachment, and an existing RBAC-enabled test vault reachable from that host.
- An approved vault-scoped secret-read role definition ID and permission to assign it.

## Shared inputs

Use PowerShell syntax on both surfaces. Stop on errors; check `$LASTEXITCODE` after CLI commands. Use only synthetic secret values and unique test names.

```powershell
$ErrorActionPreference = 'Stop'
$az = '<full-path-to-AzCLIPS-executable>'
$sub = '<test-subscription-id>'
$location = '<approved-region>'
$vault = '<existing-test-vault-name>'
$vaultId = '<existing-test-vault-resource-ID>'
$role = '<approved-secret-read-role-definition-ID>'
$run = Get-Date -Format 'yyyyMMddHHmmss'
$rg = "azclips-access-$run"
$identity = "app-identity-$run"
$secret = "app-secret-$run"
$assignment = [guid]::NewGuid().ToString()
$identityId = "/subscriptions/$sub/resourceGroups/$rg/providers/Microsoft.ManagedIdentity/userAssignedIdentities/$identity"
$fixture = Join-Path $env:TEMP "$secret.txt"
$cliScope = @('--subscription-id', $sub, '--resource-group', $rg)
$psIdentity = @{ SubscriptionId = $sub; ResourceGroupName = $rg; Name = $identity }
```

## Customer journey

Choose CLI or PowerShell for each action; do not create duplicate grants. The vault and host are pre-existing and must not be deleted.

| Step | Customer action | Commands (AzCLIPS CLI & AzCLIPS PowerShell) | Expected result / verification |
|---|---|---|---|
| **1. Create the application's identity** | Create a dedicated resource group and user-assigned managed identity. Inspect and tag it. | **AzCLIPS CLI:**<br>`& $az group create --subscription-id $sub --name $rg --location $location`<br>`& $az identity create --subscription $sub --resource-group $rg --name $identity --location $location`<br>`& $az identity list --subscription $sub --resource-group $rg`<br>`& $az identity show --ids $identityId`<br>`& $az identity update --ids $identityId --tags purpose=application-access`<br><br>**AzCLIPS PowerShell:**<br>`New-AzResourceGroup -SubscriptionId $sub -Name $rg -Location $location`<br>`New-AzUserAssignedIdentity @psIdentity -Location $location`<br>`Get-AzUserAssignedIdentity -SubscriptionId $sub -ResourceGroupName $rg`<br>`Get-AzUserAssignedIdentity @psIdentity`<br>`Update-AzUserAssignedIdentity @psIdentity -Tag @{ purpose = 'application-access' }` | Identity exists and its tag is visible. Record its `properties.principalId` and `properties.clientId` as `$principalId` and `$clientId`; these are different identifiers. |
| **2. Prepare a test secret** | Write, inspect and rotate a synthetic application value. | **Pending release confirmation; source-backed AzCLIPS CLI commands:**<br>`Set-Content -LiteralPath $fixture -Value 'azclips-synthetic-v1' -NoNewline`<br>`& $az keyvault secret set --vault-name $vault --name $secret --file $fixture`<br>`& $az keyvault secret show --vault-name $vault --name $secret`<br>`& $az keyvault secret list --vault-name $vault`<br>`Set-Content -LiteralPath $fixture -Value 'azclips-synthetic-v2' -NoNewline`<br>Repeat `secret set` to create version 2.<br>`& $az keyvault secret list-versions --vault-name $vault --name $secret`<br><br>**Pending release confirmation; source-backed AzCLIPS PowerShell commands:**<br>`Set-AzKeyVaultSecret -VaultName $vault -Name $secret -SecretValue (ConvertTo-SecureString 'azclips-synthetic-v1' -AsPlainText -Force)`<br>`Get-AzKeyVaultSecret -VaultName $vault -Name $secret`<br>`Get-AzKeyVaultSecret -VaultName $vault`<br>Repeat set with `azclips-synthetic-v2`.<br>`Get-AzKeyVaultSecret -VaultName $vault -Name $secret -IncludeVersions` | Two versions exist and metadata does not disclose the value. Latest version contains v2. Do not copy secret values into logs or issues. |
| **3. Grant and use access** | Grant secret-read access at vault scope, attach the identity to the test host, then read from that host. | **AzCLIPS CLI:**<br>`& $az role assignment create --scope $vaultId --assignee-object-id $principalId --assignee-principal-type ServicePrincipal --role $role --name $assignment`<br>`& $az role assignment list --scope $vaultId --assignee-object-id $principalId`<br><br>**AzCLIPS PowerShell:**<br>`New-AzRoleAssignment -Scope $vaultId -ObjectId $principalId -ObjectType ServicePrincipal -RoleDefinitionId $role -Name $assignment`<br>`Get-AzRoleAssignment -Scope $vaultId -ObjectId $principalId`<br><br>**Pending:** identity attachment/detachment commands for the selected host; role-definition discovery commands.<br><br>**On the Azure host, AzCLIPS CLI:**<br>`& $az login --identity --client-id $clientId --subscription-id $sub --out-profile "workload-$run"`<br>**Pending release confirmation:**<br>`$value = & $az keyvault secret show --vault-name $vault --name $secret --as-plain-text`<br><br>**On the Azure host, AzCLIPS PowerShell:**<br>`Connect-AzAccount -Identity -ClientId $clientId -SubscriptionId $sub -OutProfile "workload-$run"`<br>**Pending release confirmation:**<br>`$value = Get-AzKeyVaultSecret -VaultName $vault -Name $secret -AsPlainText` | After identity attachment and RBAC propagation, the workload reads v2 without a stored credential. Compare `$value` in memory with the synthetic expected value; do not print it. Grant creation alone is not proof of workload access. If attachment remains Pending, this checkpoint is blocked. |
| **4. Revoke and verify** | Remove the grant and retry the workload read. Exercise secret enable/disable as a separate variant. | **From the operator session, AzCLIPS CLI:**<br>`& $az role assignment delete --scope $vaultId --name $assignment`<br>`& $az role assignment list --scope $vaultId --assignee-object-id $principalId`<br><br>**AzCLIPS PowerShell:**<br>`Remove-AzRoleAssignment -Scope $vaultId -Name $assignment`<br>`Get-AzRoleAssignment -Scope $vaultId -ObjectId $principalId`<br><br>Retry the step 3 read on the host.<br><br>**Pending release confirmation, operator-only variant:**<br>`& $az keyvault secret set-attributes --vault-name $vault --name $secret --enabled false`<br>`Update-AzKeyVaultSecret -VaultName $vault -Name $secret -Enabled false`<br>Choose one; repeat with `true` to restore it.<br><br>**Pending:** generic resource inspection for permission troubleshooting. | Grant is absent and workload eventually receives the expected access-denied response. Allow for RBAC caching; record elapsed time and result rather than treating an unrelated error as success. Disabled-secret reads fail for the expected reason. |
| **5. Clean up** | Detach the identity, remove test profiles and delete the identity, group and active secret. | **Pending:** detach the test identity from the host before deletion.<br><br>**AzCLIPS CLI:**<br>`& $az identity delete --ids $identityId`<br>`& $az group delete --subscription-id $sub --name $rg`<br>`& $az group exists --subscription-id $sub --name $rg`<br>**Pending release confirmation:**<br>`& $az keyvault secret delete --vault-name $vault --name $secret --yes`<br><br>**AzCLIPS PowerShell:**<br>`Remove-AzUserAssignedIdentity @psIdentity`<br>`Remove-AzResourceGroup -SubscriptionId $sub -Name $rg -Force`<br>**Pending release confirmation:**<br>`Remove-AzKeyVaultSecret -VaultName $vault -Name $secret -Confirm:$false`<br><br>On the host, sign out and remove only `"workload-$run"` using scenario 1's profile cleanup. Remove `$fixture` if created and clear `$value` from the session. | No grant, host identity attachment, test profile, identity, resource group or active test secret remains. Secret soft-delete retention is expected; do not purge. Retain the shared vault and host. Perform cleanup after partial failures too. |

## Certificate variant

**Pending:** Engineering-confirmed AzCLIPS CLI and PowerShell commands for creating/importing, inspecting, updating and deleting a test certificate. Verify certificate metadata and expiry without exporting private keys; delete only the run-owned certificate without purge. This variant does not imply the application is configured for TLS.
