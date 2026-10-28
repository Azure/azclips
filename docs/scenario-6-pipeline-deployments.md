# Scenario 6: Run repeatable deployments from a pipeline

**User goal:** "I want a pipeline to authenticate without a browser, deploy predictably and fail clearly when a command fails."

**Successful outcome:** A non-interactive job deploys the intended resource, a repeat run does not duplicate it, failures are observable and the job cleans up its test resources and local sign-in state.

## Prerequisites

- A test pipeline agent with the AzCLIPS CLI and/or the preview Az PowerShell modules, installed as described in [Scenario 1](https://github.com/Azure/azclips/blob/docs/scenario-2-feedback/docs/scenario-1-get-started-and-profiles.md). The modules require PowerShell 7.6+.
- A test service principal with least-privilege access to the test subscription and a federated credential for your pipeline provider. The provider supplies a token file whose path is in `AZURE_FEDERATED_TOKEN_FILE`.
- The sample storage template from [Scenario 4](https://github.com/Azure/azclips/blob/docs/scenario-2-feedback/docs/scenario-4-infrastructure-deployment.md).

Never print tokens, secrets or the process environment in job output.

## User journey

Choose one command path below: **AzCLIPS CLI in Bash**, **AzCLIPS CLI in PowerShell** or **AzCLIPS PowerShell**. Each path follows the same steps.

- **1. Sign in without a browser:** Authenticate the job with a federated (OIDC) identity.
- **2. Prepare the job:** Confirm the saved sign-in context and create a uniquely named test resource group.
- **3. Deploy twice:** Run the same infrastructure deployment twice with the same inputs.
- **4. Detect a failure:** Run a lookup that is expected to fail and make the job fail if it unexpectedly succeeds.
- **5. Clean up:** Delete the group, sign out and remove the job's profile. Run this step even when an earlier step fails.

## AzCLIPS CLI in Bash

```bash
set -euo pipefail
tenant="<test-tenant-id>"
sub="<test-subscription-id>"
client_id="<application-client-ID>"
location="<region>"
template="<path-to-sample-storage.bicep>"
run=$(date +%Y%m%d%H%M%S)
profile="pipeline-$run"
rg="azclips-ci-$run"
deployment="ci-$run"
storage="clips$(openssl rand -hex 9)"
```

**1. Sign in without a browser**

```bash
federated_token="$(cat "$AZURE_FEDERATED_TOKEN_FILE")"
az login --service-principal --client-id "$client_id" --tenant-id "$tenant" --subscription-id "$sub" --federated-token "$federated_token" --out-profile "$profile"
```

**2. Prepare the job**

```bash
az profile show "$profile"
az group create --subscription-id "$sub" --name "$rg" --location "$location"
```

**3. Deploy twice**

```bash
for attempt in 1 2; do
  az deployment group create --subscription-id "$sub" --resource-group "$rg" --name "$deployment" --template-file "$template" --parameter "storageAccountName=$storage" --mode Incremental
done
az deployment group show --subscription-id "$sub" --resource-group "$rg" --name "$deployment"
```

**4. Detect a failure**

```bash
if az group show --subscription-id "$sub" --name "$rg-missing" --output json; then
  echo "Missing-resource lookup unexpectedly succeeded." >&2
  exit 1
fi
```

**5. Clean up**

```bash
az group delete --subscription-id "$sub" --name "$rg"
az group exists --subscription-id "$sub" --name "$rg"
az profile set --name "$profile"
az logout
az profile remove --name "$profile"
unset federated_token
```

## AzCLIPS CLI in PowerShell

```powershell
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true
$tenant = '<test-tenant-id>'
$sub = '<test-subscription-id>'
$clientId = '<application-client-ID>'
$location = '<region>'
$template = '<path-to-sample-storage.bicep>'
$run = Get-Date -Format 'yyyyMMddHHmmss'
$profile = "pipeline-$run"
$rg = "azclips-ci-$run"
$deployment = "ci-$run"
$storage = "clips$([guid]::NewGuid().ToString('N').Substring(0, 18))"
```

**1. Sign in without a browser**

```powershell
$federatedToken = (Get-Content -LiteralPath $env:AZURE_FEDERATED_TOKEN_FILE -Raw).Trim()
az login --service-principal --client-id $clientId --tenant-id $tenant --subscription-id $sub --federated-token $federatedToken --out-profile $profile
```

**2. Prepare the job**

```powershell
az profile show $profile
az group create --subscription-id $sub --name $rg --location $location
```

**3. Deploy twice**

```powershell
1..2 | ForEach-Object {
    az deployment group create --subscription-id $sub --resource-group $rg --name $deployment --template-file $template --parameter "storageAccountName=$storage" --mode Incremental
}
az deployment group show --subscription-id $sub --resource-group $rg --name $deployment
```

**4. Detect a failure**

```powershell
try {
    az group show --subscription-id $sub --name "$rg-missing" --output json
    throw 'Missing-resource lookup unexpectedly succeeded.'
}
catch {
    if ($_.Exception.Message -like '*unexpectedly succeeded*') { throw }
}
```

**5. Clean up**

```powershell
az group delete --subscription-id $sub --name $rg
az group exists --subscription-id $sub --name $rg
az profile set --name $profile
az logout
az profile remove --name $profile
Remove-Variable federatedToken
```

## AzCLIPS PowerShell

```powershell
$ErrorActionPreference = 'Stop'
$tenant = '<test-tenant-id>'
$sub = '<test-subscription-id>'
$clientId = '<application-client-ID>'
$location = '<region>'
$template = '<path-to-sample-storage.bicep>'
$run = Get-Date -Format 'yyyyMMddHHmmss'
$profile = "pipeline-$run"
$rg = "azclips-ci-$run"
$deployment = "ci-$run"
$storage = "clips$([guid]::NewGuid().ToString('N').Substring(0, 18))"
```

**1. Sign in without a browser**

```powershell
$federatedToken = (Get-Content -LiteralPath $env:AZURE_FEDERATED_TOKEN_FILE -Raw).Trim()
Connect-AzAccount -ServicePrincipal -ClientId $clientId -TenantId $tenant -SubscriptionId $sub -FederatedToken $federatedToken -OutProfile $profile
```

**2. Prepare the job**

```powershell
Get-AzProfile -Name $profile
New-AzResourceGroup -SubscriptionId $sub -Name $rg -Location $location
```

**3. Deploy twice**

```powershell
1..2 | ForEach-Object {
    New-AzResourceGroupDeployment -SubscriptionId $sub -ResourceGroupName $rg -Name $deployment -TemplateFile $template -Parameter "storageAccountName=$storage" -Mode Incremental
}
Get-AzResourceGroupDeployment -SubscriptionId $sub -ResourceGroupName $rg -Name $deployment
```

**4. Detect a failure**

```powershell
try {
    Get-AzResourceGroup -SubscriptionId $sub -Name "$rg-missing" -ErrorAction Stop
    throw 'Missing-resource lookup unexpectedly succeeded.'
}
catch {
    if ($_.Exception.Message -like '*unexpectedly succeeded*') { throw }
}
```

**5. Clean up**

```powershell
Remove-AzResourceGroup -SubscriptionId $sub -Name $rg -Force
Get-AzResourceGroup -SubscriptionId $sub -Name $rg -ErrorAction SilentlyContinue
Set-AzProfile -Name $profile
Disconnect-AzAccount
Remove-AzProfile -Name $profile
Remove-Variable federatedToken
```

## Other sign-in methods

Replace step 1 with one of the following. Keep secrets and certificates in the pipeline's secret store, never in source or job output.

**Managed identity**, on an agent with an assigned managed identity. Set the client ID to the managed identity's client ID.

- AzCLIPS CLI in Bash: `az login --identity --client-id "$client_id" --subscription-id "$sub" --out-profile "$profile"`
- AzCLIPS CLI in PowerShell: `az login --identity --client-id $clientId --subscription-id $sub --out-profile $profile`
- AzCLIPS PowerShell: `Connect-AzAccount -Identity -ClientId $clientId -SubscriptionId $sub -OutProfile $profile`

**Client secret or certificate**

- AzCLIPS CLI: `<client-secret or certificate sign-in command>`
- AzCLIPS PowerShell: `<client-secret or certificate sign-in command>`
