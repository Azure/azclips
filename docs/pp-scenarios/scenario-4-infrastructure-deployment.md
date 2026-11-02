# Scenario 4: Deploy and troubleshoot infrastructure as code

**User goal:** "I want to preview an infrastructure change, deploy it, understand a failure and safely remove the test environment."

**Successful outcome:** A template creates the intended resource, a repeat deployment behaves predictably, and a controlled failure produces actionable diagnostics without leaving test resources behind.

## Prerequisites

- The AzCLIPS CLI and/or the preview Az PowerShell modules, installed and signed in as described in [Scenario 1](https://github.com/Azure/azclips/blob/main/docs/pp-scenarios/scenario-1-get-started-and-profiles.md). The modules require PowerShell 7.6+.
- The Bicep CLI, and the sample [storage template](https://github.com/Azure/azclips/blob/main/tests/Scripts/Bicep/sample-storage.bicep) saved locally.

Deploy in Incremental mode into a dedicated resource group, not against shared resources.

## User journey

Choose one command path below: **AzCLIPS CLI in Bash**, **AzCLIPS CLI in PowerShell** or **AzCLIPS PowerShell**. Each path follows the same steps.

- **1. Prepare the deployment:** Create and tag a dedicated resource group.
- **2. Preview the change:** Review the proposed storage account with what-if before deploying.
- **3. Deploy and inspect:** Deploy the template and inspect the deployment.
- **4. Repeat the deployment:** Run the same deployment again and confirm it does not create a second storage account.
- **5. Diagnose a failure:** Deploy the same template into a second resource group. The storage account name is already taken, so the deployment fails. Read the error, then list the deployment's operations to find the one that failed.
- **6. Clean up:** Delete both resource groups, which removes the storage account and the deployment history.

## AzCLIPS CLI in Bash

```bash
set -euo pipefail
sub="<test-subscription-id>"
location="<region>"
template="<path-to-sample-storage.bicep>"
# Unique suffix so this run's resources don't collide
run=$(date +%Y%m%d%H%M%S)
rg="azclips-iac-$run"
rg_fail="$rg-fail"
deployment="deploy-$run"
storage="clips$(openssl rand -hex 9)"
```

**1. Prepare the deployment**

```bash
az group create --subscription-id "$sub" --name "$rg" --location "$location"
az group update --subscription-id "$sub" --resource-group "$rg" --set tags.purpose=iac-review
az group show --subscription-id "$sub" --name "$rg"
```

**2. Preview the change**

```bash
az deployment group what-if --subscription-id "$sub" --resource-group "$rg" --name "$deployment" --template-file "$template" --parameter "storageAccountName=$storage" --mode Incremental
```

**3. Deploy and inspect**

```bash
az deployment group create --subscription-id "$sub" --resource-group "$rg" --name "$deployment" --template-file "$template" --parameter "storageAccountName=$storage" --validation-level Provider --mode Incremental
az deployment group show --subscription-id "$sub" --resource-group "$rg" --name "$deployment"
az deployment group list --subscription-id "$sub" --resource-group "$rg"
```

**4. Repeat the deployment**

```bash
az deployment group create --subscription-id "$sub" --resource-group "$rg" --name "$deployment" --template-file "$template" --parameter "storageAccountName=$storage" --validation-level Provider --mode Incremental
```

**5. Diagnose a failure**

Create a second resource group and deploy the same template with the same storage account name. The name is already used by the account from step 3, so this deployment fails.

```bash
az group create --subscription-id "$sub" --name "$rg_fail" --location "$location"
if az deployment group create --subscription-id "$sub" --resource-group "$rg_fail" --name "$deployment-fail" --template-file "$template" --parameter "storageAccountName=$storage" --mode Incremental; then
  echo "The deployment unexpectedly succeeded." >&2
  exit 1
fi
```

Read the deployment's error, then list its operations to find the one that failed:

```bash
az deployment group show --subscription-id "$sub" --resource-group "$rg_fail" --name "$deployment-fail" --query properties.error
az deployment operation group list --subscription-id "$sub" --resource-group "$rg_fail" --name "$deployment-fail" --query "[?properties.provisioningState=='Failed']"
```

**6. Clean up**

```bash
az group delete --subscription-id "$sub" --name "$rg"
az group delete --subscription-id "$sub" --name "$rg_fail"
az group exists --subscription-id "$sub" --name "$rg"
az group exists --subscription-id "$sub" --name "$rg_fail"
```

## AzCLIPS CLI in PowerShell

```powershell
$ErrorActionPreference = 'Stop'
$sub = '<test-subscription-id>'
$location = '<region>'
$template = '<path-to-sample-storage.bicep>'
# Unique suffix so this run's resources don't collide
$run = Get-Date -Format 'yyyyMMddHHmmss'
$rg = "azclips-iac-$run"
$rgFail = "$rg-fail"
$deployment = "deploy-$run"
$storage = "clips$([guid]::NewGuid().ToString('N').Substring(0, 18))"
$scope = @('--subscription-id', $sub, '--resource-group', $rg)
$scopeFail = @('--subscription-id', $sub, '--resource-group', $rgFail)
```

**1. Prepare the deployment**

```powershell
az group create --subscription-id $sub --name $rg --location $location
az group update --subscription-id $sub --resource-group $rg --set tags.purpose=iac-review
az group show --subscription-id $sub --name $rg
```

**2. Preview the change**

```powershell
az deployment group what-if @scope --name $deployment --template-file $template --parameter "storageAccountName=$storage" --mode Incremental
```

**3. Deploy and inspect**

```powershell
az deployment group create @scope --name $deployment --template-file $template --parameter "storageAccountName=$storage" --validation-level Provider --mode Incremental
az deployment group show @scope --name $deployment
az deployment group list @scope
```

**4. Repeat the deployment**

```powershell
az deployment group create @scope --name $deployment --template-file $template --parameter "storageAccountName=$storage" --validation-level Provider --mode Incremental
```

**5. Diagnose a failure**

Create a second resource group and deploy the same template with the same storage account name. The name is already used by the account from step 3, so this deployment fails.

```powershell
az group create --subscription-id $sub --name $rgFail --location $location
az deployment group create @scopeFail --name "$deployment-fail" --template-file $template --parameter "storageAccountName=$storage" --mode Incremental
"Exit code: $LASTEXITCODE"
```

Read the deployment's error, then list its operations to find the one that failed:

```powershell
az deployment group show @scopeFail --name "$deployment-fail" --query properties.error
az deployment operation group list --subscription-id $sub --resource-group $rgFail --name "$deployment-fail" --query "[?properties.provisioningState=='Failed']"
```

**6. Clean up**

```powershell
az group delete --subscription-id $sub --name $rg
az group delete --subscription-id $sub --name $rgFail
az group exists --subscription-id $sub --name $rg
az group exists --subscription-id $sub --name $rgFail
```

## AzCLIPS PowerShell

```powershell
$ErrorActionPreference = 'Stop'
$sub = '<test-subscription-id>'
$location = '<region>'
$template = '<path-to-sample-storage.bicep>'
# Unique suffix so this run's resources don't collide
$run = Get-Date -Format 'yyyyMMddHHmmss'
$rg = "azclips-iac-$run"
$rgFail = "$rg-fail"
$deployment = "deploy-$run"
$storage = "clips$([guid]::NewGuid().ToString('N').Substring(0, 18))"
$psDeployment = @{ SubscriptionId = $sub; ResourceGroupName = $rg; Name = $deployment }
$psFail = @{ SubscriptionId = $sub; ResourceGroupName = $rgFail; Name = "$deployment-fail" }
```

**1. Prepare the deployment**

```powershell
New-AzResourceGroup -SubscriptionId $sub -Name $rg -Location $location
Set-AzResourceGroup -SubscriptionId $sub -Name $rg -Tag @{ purpose = 'iac-review' }
Get-AzResourceGroup -SubscriptionId $sub -Name $rg
```

**2. Preview the change**

```powershell
New-AzResourceGroupDeploymentWhatIf @psDeployment -TemplateFile $template -Parameter "storageAccountName=$storage" -Mode Incremental
```

**3. Deploy and inspect**

```powershell
New-AzResourceGroupDeployment @psDeployment -TemplateFile $template -Parameter "storageAccountName=$storage" -ValidationLevel Provider -Mode Incremental
Get-AzResourceGroupDeployment @psDeployment
```

**4. Repeat the deployment**

```powershell
New-AzResourceGroupDeployment @psDeployment -TemplateFile $template -Parameter "storageAccountName=$storage" -ValidationLevel Provider -Mode Incremental
```

**5. Diagnose a failure**

Create a second resource group and deploy the same template with the same storage account name. The name is already used by the account from step 3, so this deployment fails.

```powershell
New-AzResourceGroup -SubscriptionId $sub -Name $rgFail -Location $location
try {
    New-AzResourceGroupDeployment @psFail -TemplateFile $template -Parameter "storageAccountName=$storage" -Mode Incremental -ErrorAction Stop
    throw 'The deployment unexpectedly succeeded.'
}
catch {
    if ($_.Exception.Message -like '*unexpectedly succeeded*') { throw }
    $_.Exception.Message
}
```

Read the failed deployment, then list its operations to find the one that failed:

```powershell
Get-AzResourceGroupDeployment @psFail
Get-AzResourceGroupDeploymentOperation -SubscriptionId $sub -ResourceGroupName $rgFail -DeploymentName "$deployment-fail" | Where-Object { $_.Properties.ProvisioningState -eq 'Failed' }
```

**6. Clean up**

```powershell
Remove-AzResourceGroup -SubscriptionId $sub -Name $rg -Force
Remove-AzResourceGroup -SubscriptionId $sub -Name $rgFail -Force
Get-AzResourceGroup -SubscriptionId $sub -Name $rg -ErrorAction SilentlyContinue
Get-AzResourceGroup -SubscriptionId $sub -Name $rgFail -ErrorAction SilentlyContinue
```

## Other deployment scopes

Deployments can target a broader scope than a resource group. Use test scopes and uniquely named resources. Deleting a resource group does not remove resources deployed at a higher scope, so remove those separately.

### Management group scope

You need a test management group and permission to deploy to it. Save this empty template as `mg-empty.json` in your current folder. It deploys nothing, so the only thing to clean up is the deployment history entry.

```json
{
  "$schema": "https://schema.management.azure.com/schemas/2019-08-01/managementGroupDeploymentTemplate.json#",
  "contentVersion": "1.0.0.0",
  "parameters": {},
  "resources": []
}
```

Then run the commands for your path, using the variables from its setup block.

**AzCLIPS CLI in Bash**

```bash
mg_id="<management-group-id>"
az deployment mg create --management-group-id "$mg_id" --location "$location" --name "$deployment-mg" --template-file mg-empty.json
az deployment mg show --management-group-id "$mg_id" --name "$deployment-mg"
az deployment mg list --management-group-id "$mg_id"
az deployment mg delete --management-group-id "$mg_id" --name "$deployment-mg"
```

**AzCLIPS CLI in PowerShell**

```powershell
$mgId = '<management-group-id>'
az deployment mg create --management-group-id $mgId --location $location --name "$deployment-mg" --template-file mg-empty.json
az deployment mg show --management-group-id $mgId --name "$deployment-mg"
az deployment mg list --management-group-id $mgId
az deployment mg delete --management-group-id $mgId --name "$deployment-mg"
```

**AzCLIPS PowerShell**

```powershell
$mgId = '<management-group-id>'
New-AzManagementGroupDeployment -ManagementGroupId $mgId -Location $location -Name "$deployment-mg" -TemplateFile mg-empty.json
Get-AzManagementGroupDeployment -ManagementGroupId $mgId -Name "$deployment-mg"
Get-AzManagementGroupDeployment -ManagementGroupId $mgId
Remove-AzManagementGroupDeployment -ManagementGroupId $mgId -Name "$deployment-mg" -Confirm:$false
```

### Subscription and tenant scope

- Subscription scope: `<subscription deployment commands>`
- Tenant scope: `<tenant deployment commands>`
