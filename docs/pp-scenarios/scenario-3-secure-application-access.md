# Scenario 3: Give an application secure access to Azure resources

**User goal:** "I want my application to read a Key Vault secret using a managed identity instead of an embedded password."

**Successful outcome:** The workload reads a test secret using its assigned identity, loses access after the grant is revoked, and leaves no test identity or active test secret behind.

## Prerequisites

- The AzCLIPS CLI and/or the preview Az PowerShell modules, installed and signed in as described in [Scenario 1](https://github.com/Azure/azclips/blob/main/docs/scenario-1-get-started-and-profiles.md). The modules require PowerShell 7.6+.
- An Azure host, such as a VM, that can use a user-assigned managed identity and has the AzCLIPS CLI or modules installed.
- An existing test Key Vault that uses Azure role-based access control and is reachable from that host.
- The role definition ID of a built-in role that grants secret read access, and permission to assign it.

Use only synthetic secret values. Do not print or log secret values.

## User journey

Choose one command path below: **AzCLIPS CLI in Bash**, **AzCLIPS CLI in PowerShell** or **AzCLIPS PowerShell**. Each path follows the same steps. Run steps 1 to 3 and 5 to 6 from your workstation, and step 4 on the Azure host.

- **1. Create the application's identity:** Create a dedicated resource group and a user-assigned managed identity, and record its principal ID and client ID.
- **2. Store a test secret:** Write a synthetic secret to the existing vault and read its metadata.
- **3. Grant access:** Assign the secret-read role to the identity at the vault scope and attach the identity to the host.
- **4. Read the secret as the application:** On the host, sign in with the managed identity and read the secret.
- **5. Revoke access:** Remove the role assignment and retry the read, which should now be denied.
- **6. Clean up:** Detach and delete the identity, delete the resource group and delete the test secret.

Allow time for role changes to take effect before verifying a grant or a revocation. Step 4 runs on the host, so set the same subscription, vault, secret, client ID and run values there.

## AzCLIPS CLI in Bash

```bash
set -euo pipefail
sub="<test-subscription-id>"
location="<region>"
vault="<existing-test-vault-name>"
vault_id="<existing-test-vault-resource-ID>"
role="<secret-read-role-definition-ID>"
# Unique suffix so this run's resources don't collide
run=$(date +%Y%m%d%H%M%S)
rg="azclips-access-$run"
identity="app-identity-$run"
secret="app-secret-$run"
assignment=$(uuidgen)
identity_id="/subscriptions/$sub/resourceGroups/$rg/providers/Microsoft.ManagedIdentity/userAssignedIdentities/$identity"
fixture="/tmp/$secret.txt"
```

**1. Create the application's identity**

```bash
az group create --subscription-id "$sub" --name "$rg" --location "$location"
az identity create --subscription "$sub" --resource-group "$rg" --name "$identity" --location "$location"
az identity show --ids "$identity_id"
principal_id=$(az identity show --ids "$identity_id" --query properties.principalId --output tsv)
client_id=$(az identity show --ids "$identity_id" --query properties.clientId --output tsv)
```

**2. Store a test secret**

```bash
printf 'azclips-synthetic' > "$fixture"
az keyvault secret set --vault-name "$vault" --name "$secret" --file "$fixture"
az keyvault secret show --vault-name "$vault" --name "$secret"
```

**3. Grant access**

```bash
az role assignment create --scope "$vault_id" --assignee-object-id "$principal_id" --assignee-principal-type ServicePrincipal --role "$role" --name "$assignment"
az role assignment list --scope "$vault_id" --assignee-object-id "$principal_id"
```

Attach the identity to the host: `<command to attach the identity to the host>`

**4. Read the secret as the application**

On the host:

```bash
az login --identity --client-id "$client_id" --subscription-id "$sub" --out-profile "workload-$run"
value=$(az keyvault secret show --vault-name "$vault" --name "$secret" --as-plain-text)
test "$value" = "azclips-synthetic"
```

**5. Revoke access**

```bash
az role assignment delete --scope "$vault_id" --name "$assignment"
az role assignment list --scope "$vault_id" --assignee-object-id "$principal_id"
```

On the host, repeat the read from step 4. It should now be denied.

**6. Clean up**

Detach the identity from the host: `<command to detach the identity from the host>`

```bash
az identity delete --ids "$identity_id"
az group delete --subscription-id "$sub" --name "$rg"
az group exists --subscription-id "$sub" --name "$rg"
az keyvault secret delete --vault-name "$vault" --name "$secret" --yes
rm -f "$fixture"
```

On the host, sign out and remove the `workload-$run` profile with `az logout` and `az profile remove --name "workload-$run"`.

## AzCLIPS CLI in PowerShell

```powershell
$ErrorActionPreference = 'Stop'
$sub = '<test-subscription-id>'
$location = '<region>'
$vault = '<existing-test-vault-name>'
$vaultId = '<existing-test-vault-resource-ID>'
$role = '<secret-read-role-definition-ID>'
# Unique suffix so this run's resources don't collide
$run = Get-Date -Format 'yyyyMMddHHmmss'
$rg = "azclips-access-$run"
$identity = "app-identity-$run"
$secret = "app-secret-$run"
$assignment = [guid]::NewGuid().ToString()
$identityId = "/subscriptions/$sub/resourceGroups/$rg/providers/Microsoft.ManagedIdentity/userAssignedIdentities/$identity"
$fixture = Join-Path ([IO.Path]::GetTempPath()) "$secret.txt"
```

**1. Create the application's identity**

```powershell
az group create --subscription-id $sub --name $rg --location $location
az identity create --subscription $sub --resource-group $rg --name $identity --location $location
az identity show --ids $identityId
$principalId = az identity show --ids $identityId --query properties.principalId --output tsv
$clientId = az identity show --ids $identityId --query properties.clientId --output tsv
```

**2. Store a test secret**

```powershell
Set-Content -LiteralPath $fixture -Value 'azclips-synthetic' -NoNewline
az keyvault secret set --vault-name $vault --name $secret --file $fixture
az keyvault secret show --vault-name $vault --name $secret
```

**3. Grant access**

```powershell
az role assignment create --scope $vaultId --assignee-object-id $principalId --assignee-principal-type ServicePrincipal --role $role --name $assignment
az role assignment list --scope $vaultId --assignee-object-id $principalId
```

Attach the identity to the host: `<command to attach the identity to the host>`

**4. Read the secret as the application**

On the host:

```powershell
az login --identity --client-id $clientId --subscription-id $sub --out-profile "workload-$run"
$value = az keyvault secret show --vault-name $vault --name $secret --as-plain-text
if ($value -ne 'azclips-synthetic') { throw 'Secret value mismatch.' }
```

**5. Revoke access**

```powershell
az role assignment delete --scope $vaultId --name $assignment
az role assignment list --scope $vaultId --assignee-object-id $principalId
```

On the host, repeat the read from step 4. It should now be denied.

**6. Clean up**

Detach the identity from the host: `<command to detach the identity from the host>`

```powershell
az identity delete --ids $identityId
az group delete --subscription-id $sub --name $rg
az group exists --subscription-id $sub --name $rg
az keyvault secret delete --vault-name $vault --name $secret --yes
Remove-Item -LiteralPath $fixture -ErrorAction SilentlyContinue
Remove-Variable value -ErrorAction SilentlyContinue
```

On the host, sign out and remove the `workload-$run` profile with `az logout` and `az profile remove --name "workload-$run"`.

## AzCLIPS PowerShell

```powershell
$ErrorActionPreference = 'Stop'
$sub = '<test-subscription-id>'
$location = '<region>'
$vault = '<existing-test-vault-name>'
$vaultId = '<existing-test-vault-resource-ID>'
$role = '<secret-read-role-definition-ID>'
# Unique suffix so this run's resources don't collide
$run = Get-Date -Format 'yyyyMMddHHmmss'
$rg = "azclips-access-$run"
$identity = "app-identity-$run"
$secret = "app-secret-$run"
$assignment = [guid]::NewGuid().ToString()
$psIdentity = @{ SubscriptionId = $sub; ResourceGroupName = $rg; Name = $identity }
```

**1. Create the application's identity**

```powershell
New-AzResourceGroup -SubscriptionId $sub -Name $rg -Location $location
New-AzUserAssignedIdentity @psIdentity -Location $location
$id = Get-AzUserAssignedIdentity @psIdentity
$principalId = $id.Properties.PrincipalId
$clientId = $id.Properties.ClientId
```

**2. Store a test secret**

```powershell
Set-AzKeyVaultSecret -VaultName $vault -Name $secret -SecretValue (ConvertTo-SecureString 'azclips-synthetic' -AsPlainText -Force)
Get-AzKeyVaultSecret -VaultName $vault -Name $secret
```

**3. Grant access**

```powershell
New-AzRoleAssignment -Scope $vaultId -ObjectId $principalId -ObjectType ServicePrincipal -RoleDefinitionId $role -Name $assignment
Get-AzRoleAssignment -Scope $vaultId -ObjectId $principalId
```

Attach the identity to the host: `<command to attach the identity to the host>`

**4. Read the secret as the application**

On the host:

```powershell
Connect-AzAccount -Identity -ClientId $clientId -SubscriptionId $sub -OutProfile "workload-$run"
$value = Get-AzKeyVaultSecret -VaultName $vault -Name $secret -AsPlainText
if ($value -ne 'azclips-synthetic') { throw 'Secret value mismatch.' }
```

**5. Revoke access**

```powershell
Remove-AzRoleAssignment -Scope $vaultId -Name $assignment
Get-AzRoleAssignment -Scope $vaultId -ObjectId $principalId
```

On the host, repeat the read from step 4. It should now be denied.

**6. Clean up**

Detach the identity from the host: `<command to detach the identity from the host>`

```powershell
Remove-AzUserAssignedIdentity @psIdentity
Remove-AzResourceGroup -SubscriptionId $sub -Name $rg -Force
Remove-AzKeyVaultSecret -VaultName $vault -Name $secret -Confirm:$false
Remove-Variable value -ErrorAction SilentlyContinue
```

On the host, sign out and remove the `workload-$run` profile with `Disconnect-AzAccount` and `Remove-AzProfile -Name "workload-$run"`.
