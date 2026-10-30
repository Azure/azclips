# Scenario 4: Deploy and troubleshoot infrastructure as code

**User goal:** "I want to preview an infrastructure change, deploy it, understand a failure and safely remove the test environment."

**Successful outcome:** A template creates the intended resource, a repeat deployment behaves predictably, and a controlled failure produces actionable diagnostics without leaving test resources behind.

## Prerequisites

- The AzCLIPS CLI and/or the preview Az PowerShell modules, installed and signed in as described in [Scenario 1](https://github.com/Azure/azclips/blob/main/docs/scenario-1-get-started-and-profiles.md). The modules require PowerShell 7.6+.
- The Bicep CLI, and the sample [storage template](https://github.com/Azure/azclips/blob/main/tests/Scripts/Bicep/sample-storage.bicep) saved locally.

Deploy in Incremental mode into a dedicated resource group, not against shared resources.

## User journey

Choose one command path below: **AzCLIPS CLI in Bash**, **AzCLIPS CLI in PowerShell** or **AzCLIPS PowerShell**. Each path follows the same steps.

- **1. Prepare the deployment:** Create and tag a dedicated resource group.
- **2. Preview the change:** Review the proposed storage account with what-if before deploying.
- **3. Deploy and inspect:** Deploy the template and inspect the deployment.
- **4. Repeat the deployment:** Run the same deployment again and confirm it does not create a second storage account.
- **5. Diagnose a failure:** Run a deployment with an invalid mode, read the error and inspect the deployment operations.
- **6. Clean up:** Delete the resource group, which removes the deployed storage account and the deployment history.

## AzCLIPS CLI in Bash

```bash
set -euo pipefail
sub="<test-subscription-id>"
location="<region>"
template="<path-to-sample-storage.bicep>"
# Unique suffix so this run's resources don't collide
run=$(date +%Y%m%d%H%M%S)
rg="azclips-iac-$run"
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

```bash
if az deployment group create --subscription-id "$sub" --resource-group "$rg" --name "$deployment-invalid" --template-file "$template" --parameter "storageAccountName=$storage" --mode Foo; then
  echo "The invalid deployment unexpectedly succeeded." >&2
  exit 1
fi
```

Inspect the deployment operations: `<deployment operations command>`

**6. Clean up**

```bash
az group delete --subscription-id "$sub" --name "$rg"
az group exists --subscription-id "$sub" --name "$rg"
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
$deployment = "deploy-$run"
$storage = "clips$([guid]::NewGuid().ToString('N').Substring(0, 18))"
$scope = @('--subscription-id', $sub, '--resource-group', $rg)
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

```powershell
az deployment group create @scope --name "$deployment-invalid" --template-file $template --parameter "storageAccountName=$storage" --mode Foo
"Exit code: $LASTEXITCODE"
```

The command fails and reports the allowed values for the mode. Inspect the deployment operations: `<deployment operations command>`

**6. Clean up**

```powershell
az group delete --subscription-id $sub --name $rg
az group exists --subscription-id $sub --name $rg
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
$deployment = "deploy-$run"
$storage = "clips$([guid]::NewGuid().ToString('N').Substring(0, 18))"
$psDeployment = @{ SubscriptionId = $sub; ResourceGroupName = $rg; Name = $deployment }
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

```powershell
try {
    New-AzResourceGroupDeployment -SubscriptionId $sub -ResourceGroupName $rg -Name "$deployment-invalid" -TemplateFile $template -Parameter "storageAccountName=$storage" -Mode Foo
    throw 'The invalid deployment unexpectedly succeeded.'
}
catch {
    if ($_.Exception.Message -like '*unexpectedly succeeded*') { throw }
    $_.Exception.Message
}
```

Inspect the deployment operations: `<deployment operations command>`

**6. Clean up**

```powershell
Remove-AzResourceGroup -SubscriptionId $sub -Name $rg -Force
Get-AzResourceGroup -SubscriptionId $sub -Name $rg -ErrorAction SilentlyContinue
```

## Other deployment scopes

Repeat the preview, deploy, inspect and clean-up steps at a broader scope, using a template written for that scope. Use test scopes and uniquely named resources. Deleting the resource group does not remove resources deployed at a higher scope, so remove those separately.

- Subscription scope: `<subscription deployment commands>`
- Management group scope: `<management group deployment commands>`
- Tenant scope: `<tenant deployment commands>`
