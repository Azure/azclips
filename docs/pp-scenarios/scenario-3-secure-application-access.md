# Scenario 3: Give an application secure access to Azure resources

**User goal:** "I want my application to read a Key Vault secret using a managed identity instead of an embedded password."

**Successful outcome:** The workload reads a test secret using its assigned identity, loses access after the grant is revoked, and leaves no test identity or active test secret behind.

## Prerequisites

- The AzCLIPS CLI and/or the preview Az PowerShell modules, installed and signed in as described in [Scenario 1](https://github.com/Azure/azclips/blob/main/docs/pp-scenarios/scenario-1-get-started-and-profiles.md). The modules require PowerShell 7.6+.
- A dedicated test VM in Azure to act as the application's host. It needs the latest AzCLIPS CLI (for the CLI paths) or the latest preview Az PowerShell modules with PowerShell 7.6+ (for the PowerShell path), installed as in Scenario 1, and network access to the vault. Step 6 clears every managed identity on it, so do not use a VM that has other identities.
- An existing test Key Vault that uses Azure role-based access control. This scenario does not create or delete it.
- The role definition ID of a built-in role that grants secret read access, and permission to assign it and to update the host VM.

Use only synthetic secret values. Do not print or log secret values.

## User journey

Choose one command path below: **AzCLIPS CLI in Bash**, **AzCLIPS CLI in PowerShell** or **AzCLIPS PowerShell**. Each path follows the same steps.

- **1. Create the application's identity (workstation):** Create a dedicated resource group and a user-assigned managed identity, and record its principal ID and client ID.
- **2. Store a test secret (workstation):** Write a synthetic secret to the existing vault and read its metadata.
- **3. Grant access and attach the identity (workstation):** Assign the secret-read role to the identity at the vault scope, attach the identity to the host with a REST request, and print the values the host needs.
- **4. Read the secret as the application (Azure host):** Sign in with the managed identity and read the secret.
- **5. Revoke access (workstation, then Azure host):** Remove the role assignment on the workstation, then retry the read on the host, which should now be denied.
- **6. Clean up (Azure host, then workstation):** On the host, sign out and remove its profile. On the workstation, delete the test secret, detach and delete the identity, and delete the resource group.

Allow a few minutes for role and identity changes to take effect before verifying a grant or a revocation.

## AzCLIPS CLI in Bash

On your workstation:

```bash
set -euo pipefail
sub="<test-subscription-id>"
location="<region>"
vault="<existing-test-vault-name>"
vault_id="<existing-test-vault-resource-ID>"
role="<secret-read-role-definition-ID>"
host_group="<host-VM-resource-group>"
host_vm="<host-VM-name>"
# Unique suffix so this run's resources don't collide
run=$(date +%Y%m%d%H%M%S)
rg="azclips-access-$run"
identity="app-identity-$run"
secret="app-secret-$run"
assignment=$(uuidgen)
identity_id="/subscriptions/$sub/resourceGroups/$rg/providers/Microsoft.ManagedIdentity/userAssignedIdentities/$identity"
host_uri="/subscriptions/$sub/resourceGroups/$host_group/providers/Microsoft.Compute/virtualMachines/$host_vm?api-version=2024-07-01"
fixture="/tmp/$secret.txt"
```

**1. Create the application's identity (workstation)**

```bash
az group create --subscription-id "$sub" --name "$rg" --location "$location"
az identity create --subscription "$sub" --resource-group "$rg" --name "$identity" --location "$location"
az identity show --ids "$identity_id"
principal_id=$(az identity show --ids "$identity_id" --query properties.principalId --output tsv)
client_id=$(az identity show --ids "$identity_id" --query properties.clientId --output tsv)
```

**2. Store a test secret (workstation)**

```bash
printf 'azclips-synthetic' > "$fixture"
az keyvault secret set --vault-name "$vault" --name "$secret" --file "$fixture"
az keyvault secret show --vault-name "$vault" --name "$secret"
```

**3. Grant access and attach the identity (workstation)**

```bash
az role assignment create --scope "$vault_id" --assignee-object-id "$principal_id" --assignee-principal-type ServicePrincipal --role "$role" --name "$assignment"
az role assignment list --scope "$vault_id" --assignee-object-id "$principal_id"
```

Attach the identity to the host VM with a REST `PATCH`. The request uses API version `2024-07-01` for virtual machines.

```bash
az rest --method PATCH --uri "$host_uri" --body "{\"identity\":{\"type\":\"UserAssigned\",\"userAssignedIdentities\":{\"$identity_id\":{}}}}" --headers Content-Type=application/json
az rest --method GET --uri "$host_uri" --query json.identity.type --output tsv
```

The last command prints `UserAssigned`. Now print the values the host needs:

```bash
printf 'Subscription: %s\nVault: %s\nSecret: %s\nClient ID: %s\n' "$sub" "$vault" "$secret" "$client_id"
```

**4. Read the secret as the application (Azure host)**

On the Azure host, paste the four values you just printed:

```bash
sub="<subscription-ID>"
vault="<vault-name>"
secret="<secret-name>"
client_id="<identity-client-ID>"
az login --identity --client-id "$client_id" --subscription-id "$sub" --out-profile workload
value=$(az keyvault secret show --vault-name "$vault" --name "$secret" --as-plain-text)
test "$value" = "azclips-synthetic"
```

**5. Revoke access**

On your workstation, remove the role assignment:

```bash
az role assignment delete --scope "$vault_id" --name "$assignment"
az role assignment list --scope "$vault_id" --assignee-object-id "$principal_id"
```

On the Azure host, repeat the read. It should now be denied. If you still see the value, wait a few minutes and run it again.

```bash
az keyvault secret show --vault-name "$vault" --name "$secret" --as-plain-text
```

**6. Clean up**

First, on the Azure host, sign out and remove the profile:

```bash
az logout
az profile remove --name workload
unset value
```

Then, on your workstation, delete the secret, detach the identity from the host and delete the identity. Delete the resource group last, because it holds the identity.

```bash
az keyvault secret delete --vault-name "$vault" --name "$secret" --yes
az rest --method PATCH --uri "$host_uri" --body '{"identity":{"type":"None"}}' --headers Content-Type=application/json
az identity delete --ids "$identity_id"
az group delete --subscription-id "$sub" --name "$rg"
az group exists --subscription-id "$sub" --name "$rg"
rm -f "$fixture"
```

This removes everything the scenario created. It does not delete the Key Vault or the host VM. The role assignment was already removed in step 5.

## AzCLIPS CLI in PowerShell

On your workstation:

```powershell
$ErrorActionPreference = 'Stop'
$sub = '<test-subscription-id>'
$location = '<region>'
$vault = '<existing-test-vault-name>'
$vaultId = '<existing-test-vault-resource-ID>'
$role = '<secret-read-role-definition-ID>'
$hostGroup = '<host-VM-resource-group>'
$hostVm = '<host-VM-name>'
# Unique suffix so this run's resources don't collide
$run = Get-Date -Format 'yyyyMMddHHmmss'
$rg = "azclips-access-$run"
$identity = "app-identity-$run"
$secret = "app-secret-$run"
$assignment = [guid]::NewGuid().ToString()
$identityId = "/subscriptions/$sub/resourceGroups/$rg/providers/Microsoft.ManagedIdentity/userAssignedIdentities/$identity"
$hostUri = "/subscriptions/$sub/resourceGroups/$hostGroup/providers/Microsoft.Compute/virtualMachines/${hostVm}?api-version=2024-07-01"
$fixture = Join-Path ([IO.Path]::GetTempPath()) "$secret.txt"
```

**1. Create the application's identity (workstation)**

```powershell
az group create --subscription-id $sub --name $rg --location $location
az identity create --subscription $sub --resource-group $rg --name $identity --location $location
az identity show --ids $identityId
$principalId = az identity show --ids $identityId --query properties.principalId --output tsv
$clientId = az identity show --ids $identityId --query properties.clientId --output tsv
```

**2. Store a test secret (workstation)**

```powershell
Set-Content -LiteralPath $fixture -Value 'azclips-synthetic' -NoNewline
az keyvault secret set --vault-name $vault --name $secret --file $fixture
az keyvault secret show --vault-name $vault --name $secret
```

**3. Grant access and attach the identity (workstation)**

```powershell
az role assignment create --scope $vaultId --assignee-object-id $principalId --assignee-principal-type ServicePrincipal --role $role --name $assignment
az role assignment list --scope $vaultId --assignee-object-id $principalId
```

Attach the identity to the host VM with a REST `PATCH`. The request uses API version `2024-07-01` for virtual machines.

```powershell
$attach = @{ identity = @{ type = 'UserAssigned'; userAssignedIdentities = @{ $identityId = @{} } } } | ConvertTo-Json -Depth 5 -Compress
az rest --method PATCH --uri $hostUri --body $attach --headers Content-Type=application/json
az rest --method GET --uri $hostUri --query identity.type --output tsv
```

The last command prints `UserAssigned`. Now print the values the host needs:

```powershell
[pscustomobject]@{ Subscription = $sub; Vault = $vault; Secret = $secret; ClientId = $clientId } | Format-List
```

**4. Read the secret as the application (Azure host)**

On the Azure host, paste the four values you just printed:

```powershell
$sub = '<subscription-ID>'
$vault = '<vault-name>'
$secret = '<secret-name>'
$clientId = '<identity-client-ID>'
az login --identity --client-id $clientId --subscription-id $sub --out-profile workload
$value = az keyvault secret show --vault-name $vault --name $secret --as-plain-text
if ($value -ne 'azclips-synthetic') { throw 'Secret value mismatch.' }
```

**5. Revoke access**

On your workstation, remove the role assignment:

```powershell
az role assignment delete --scope $vaultId --name $assignment
az role assignment list --scope $vaultId --assignee-object-id $principalId
```

On the Azure host, repeat the read. It should now be denied. If you still see the value, wait a few minutes and run it again.

```powershell
az keyvault secret show --vault-name $vault --name $secret --as-plain-text
```

**6. Clean up**

First, on the Azure host, sign out and remove the profile:

```powershell
az logout
az profile remove --name workload
Remove-Variable value -ErrorAction SilentlyContinue
```

Then, on your workstation, delete the secret, detach the identity from the host and delete the identity. Delete the resource group last, because it holds the identity.

```powershell
az keyvault secret delete --vault-name $vault --name $secret --yes
az rest --method PATCH --uri $hostUri --body '{"identity":{"type":"None"}}' --headers Content-Type=application/json
az identity delete --ids $identityId
az group delete --subscription-id $sub --name $rg
az group exists --subscription-id $sub --name $rg
Remove-Item -LiteralPath $fixture -ErrorAction SilentlyContinue
```

This removes everything the scenario created. It does not delete the Key Vault or the host VM. The role assignment was already removed in step 5.

## AzCLIPS PowerShell

On your workstation:

```powershell
$ErrorActionPreference = 'Stop'
$sub = '<test-subscription-id>'
$location = '<region>'
$vault = '<existing-test-vault-name>'
$vaultId = '<existing-test-vault-resource-ID>'
$role = '<secret-read-role-definition-ID>'
$hostGroup = '<host-VM-resource-group>'
$hostVm = '<host-VM-name>'
# Unique suffix so this run's resources don't collide
$run = Get-Date -Format 'yyyyMMddHHmmss'
$rg = "azclips-access-$run"
$identity = "app-identity-$run"
$secret = "app-secret-$run"
$assignment = [guid]::NewGuid().ToString()
$psIdentity = @{ SubscriptionId = $sub; ResourceGroupName = $rg; Name = $identity }
$identityId = "/subscriptions/$sub/resourceGroups/$rg/providers/Microsoft.ManagedIdentity/userAssignedIdentities/$identity"
$hostUri = "/subscriptions/$sub/resourceGroups/$hostGroup/providers/Microsoft.Compute/virtualMachines/${hostVm}?api-version=2024-07-01"
```

**1. Create the application's identity (workstation)**

```powershell
New-AzResourceGroup -SubscriptionId $sub -Name $rg -Location $location
New-AzUserAssignedIdentity @psIdentity -Location $location
$id = Get-AzUserAssignedIdentity @psIdentity
$principalId = $id.Properties.PrincipalId
$clientId = $id.Properties.ClientId
```

**2. Store a test secret (workstation)**

```powershell
Set-AzKeyVaultSecret -VaultName $vault -Name $secret -SecretValue (ConvertTo-SecureString 'azclips-synthetic' -AsPlainText -Force)
Get-AzKeyVaultSecret -VaultName $vault -Name $secret
```

**3. Grant access and attach the identity (workstation)**

```powershell
New-AzRoleAssignment -Scope $vaultId -ObjectId $principalId -ObjectType ServicePrincipal -RoleDefinitionId $role -Name $assignment
Get-AzRoleAssignment -Scope $vaultId -ObjectId $principalId
```

Attach the identity to the host VM with a REST `PATCH`. The request uses API version `2024-07-01` for virtual machines.

```powershell
$attach = @{ identity = @{ type = 'UserAssigned'; userAssignedIdentities = @{ $identityId = @{} } } } | ConvertTo-Json -Depth 5 -Compress
Invoke-AzRestMethod -Method PATCH -Uri $hostUri -Payload $attach -Headers @{ 'Content-Type' = 'application/json' }
((Invoke-AzRestMethod -Method GET -Uri $hostUri).Content | ConvertFrom-Json).identity.type
```

The last command prints `UserAssigned`. Now print the values the host needs:

```powershell
[pscustomobject]@{ Subscription = $sub; Vault = $vault; Secret = $secret; ClientId = $clientId } | Format-List
```

**4. Read the secret as the application (Azure host)**

On the Azure host, paste the four values you just printed:

```powershell
$sub = '<subscription-ID>'
$vault = '<vault-name>'
$secret = '<secret-name>'
$clientId = '<identity-client-ID>'
Connect-AzAccount -Identity -ClientId $clientId -SubscriptionId $sub -OutProfile workload
$value = Get-AzKeyVaultSecret -VaultName $vault -Name $secret -AsPlainText
if ($value -ne 'azclips-synthetic') { throw 'Secret value mismatch.' }
```

**5. Revoke access**

On your workstation, remove the role assignment:

```powershell
Remove-AzRoleAssignment -Scope $vaultId -Name $assignment
Get-AzRoleAssignment -Scope $vaultId -ObjectId $principalId
```

On the Azure host, repeat the read. It should now be denied. If you still see the value, wait a few minutes and run it again.

```powershell
Get-AzKeyVaultSecret -VaultName $vault -Name $secret -AsPlainText
```

**6. Clean up**

First, on the Azure host, sign out and remove the profile:

```powershell
Disconnect-AzAccount
Remove-AzProfile -Name workload
Remove-Variable value -ErrorAction SilentlyContinue
```

Then, on your workstation, delete the secret, detach the identity from the host and delete the identity. Delete the resource group last, because it holds the identity.

```powershell
Remove-AzKeyVaultSecret -VaultName $vault -Name $secret -Confirm:$false
Invoke-AzRestMethod -Method PATCH -Uri $hostUri -Payload '{"identity":{"type":"None"}}' -Headers @{ 'Content-Type' = 'application/json' }
Remove-AzUserAssignedIdentity @psIdentity
Remove-AzResourceGroup -SubscriptionId $sub -Name $rg -Force
Get-AzResourceGroup -SubscriptionId $sub -Name $rg -ErrorAction SilentlyContinue
```

This removes everything the scenario created. It does not delete the Key Vault or the host VM. The role assignment was already removed in step 5.
