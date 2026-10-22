# Scenario 4: Deploy and troubleshoot infrastructure as code

**October 21 scope note:** the universal long-running-operation/idempotency contract is deferred to GA, not Pending PP delivery. Continue to verify the actual deployment's terminal result and explicitly scoped deployment cancellation; do not make cross-command guarantees.

**Customer goal:** "I want to preview an infrastructure change, deploy it, understand a failure and safely remove the test environment."

**Successful outcome:** A template creates the intended resource, a repeat deployment behaves predictably, and a controlled failure produces actionable diagnostics without leaving test resources behind.

## Prerequisites

- AzCLIPS CLI and preview PowerShell modules installed and authenticated in PowerShell 7.6+.
- Repository checkout and Bicep CLI available for the supplied Bicep fixture.

## Shared inputs

Commands use PowerShell syntax. Stop on unexpected errors; check `$LASTEXITCODE` after CLI commands. **Pending** deployment capabilities retain source-backed command examples where available.

```powershell
$ErrorActionPreference = 'Stop'
$az = '<full-path-to-AzCLIPS-executable>'
$repo = '<repository-root>'
$sub = '<test-subscription-id>'
$location = '<approved-region>'
$run = Get-Date -Format 'yyyyMMddHHmmss'
$rg = "azclips-iac-$run"
$deployment = "deploy-$run"
$storage = "clips$([guid]::NewGuid().ToString('N').Substring(0, 18))"
$template = Join-Path $repo 'tests\Scripts\Bicep\sample-storage.bicep'
$cliScope = @('--subscription-id', $sub, '--resource-group', $rg)
$psDeployment = @{ SubscriptionId = $sub; ResourceGroupName = $rg; Name = $deployment }
```

## Customer journey

CLI and PowerShell commands are alternatives. Use Incremental mode in a dedicated group, not Complete mode against shared resources.

| Step | Customer action | Commands (AzCLIPS CLI & AzCLIPS PowerShell) | Expected result / verification |
|---|---|---|---|
| **1. Prepare the deployment** | Create and tag a dedicated resource group. | **AzCLIPS CLI:**<br>`& $az group create --subscription-id $sub --name $rg --location $location`<br>`& $az group update --subscription-id $sub --resource-group $rg --set tags.purpose=iac-review`<br>`& $az group show --subscription-id $sub --name $rg`<br><br>**AzCLIPS PowerShell:**<br>`New-AzResourceGroup -SubscriptionId $sub -Name $rg -Location $location`<br>`Set-AzResourceGroup -SubscriptionId $sub -Name $rg -Tag @{ purpose = 'iac-review' }`<br>`Get-AzResourceGroup -SubscriptionId $sub -Name $rg` | Group exists with the intended tag and location. No storage account has been deployed yet. |
| **2. Preview and validate** | Review the proposed storage-account creation before deploying. | **Pending release confirmation, AzCLIPS CLI:**<br>`& $az deployment group what-if @cliScope --name $deployment --template-file $template --parameter "storageAccountName=$storage" --mode Incremental`<br><br>**Pending release confirmation, AzCLIPS PowerShell:**<br>`New-AzResourceGroupDeploymentWhatIf @psDeployment -TemplateFile $template -Parameter "storageAccountName=$storage" -Mode Incremental`<br><br>**Pending:** standalone validation commands on both surfaces. | What-if predicts the intended storage account and does not create it. Validation errors identify the offending input. Known what-if defects must be recorded, not treated as an approved preview. |
| **3. Deploy and inspect** | Deploy the template and inspect the resulting deployment and resource. | **Pending release confirmation, AzCLIPS CLI:**<br>`& $az deployment group create @cliScope --name $deployment --template-file $template --parameter "storageAccountName=$storage" --validation-level Provider --mode Incremental`<br><br>**Pending release confirmation, AzCLIPS PowerShell:**<br>`New-AzResourceGroupDeployment @psDeployment -TemplateFile $template -Parameter "storageAccountName=$storage" -ValidationLevel Provider -Mode Incremental`<br><br>**Pending:** deployment show/list/export, deployment-operation inspection and storage-account inspection commands on both surfaces. | Deployment reaches Succeeded and returns the expected storage-account resource ID. Confirm the actual resource exists; a submitted request alone is insufficient. Exported templates contain no secrets. |
| **4. Repeat and diagnose** | Repeat the same deployment, then exercise a separate invalid-input case and inspect diagnostics. | Repeat step 3 with the same names and parameters.<br><br>**Pending release confirmation, controlled CLI failure:**<br>`& $az deployment group create @cliScope --name "$deployment-invalid" --template-file $template --parameter "storageAccountName=$storage" --mode Foo`<br><br>**Pending release confirmation, controlled PowerShell failure:**<br>`New-AzResourceGroupDeployment -SubscriptionId $sub -ResourceGroupName $rg -Name "$deployment-invalid" -TemplateFile $template -Parameter "storageAccountName=$storage" -Mode Foo`<br><br>**Pending:** server-side deployment-failure fixture, operation troubleshooting, in-progress cancellation and no-wait completion commands. | Repeat deployment does not create a second account. Invalid mode is rejected with the allowed values before deployment. CLI reports failure; PowerShell raises an error. Compilation/input failure is distinct from an ARM deployment failure; the latter needs its own fixture. Cancellation does not imply rollback. |
| **5. Clean up** | Remove deployment history and all resources created by the template. | **Pending:** deployment delete commands on both surfaces.<br><br>**AzCLIPS CLI:**<br>`& $az group delete --subscription-id $sub --name $rg`<br>`& $az group exists --subscription-id $sub --name $rg`<br><br>**AzCLIPS PowerShell:**<br>`Remove-AzResourceGroup -SubscriptionId $sub -Name $rg -Force`<br>`Get-AzResourceGroup -SubscriptionId $sub -Name $rg` | Group and deployed storage account are absent. Deleting deployment history alone does not delete the deployed resources. Clean up after partial failures, including canceled deployments. |

## Scope variants

**Pending:** repeat the preview/validate/create/inspect/cancel/export/delete journey at subscription, management-group and tenant scope with Engineering-provided, scope-correct templates and commands.

- Use approved test scopes and uniquely named resources; do not deploy policy or RBAC changes to production scopes.
- Verify location requirements, target IDs, operations and terminal state separately for each scope.
- Record all created resources. Remove resources as well as deployment history; deleting the scenario's resource group cannot clean up every higher-scope resource.
- Parameter-file and inline-override variants must resolve to the same intended inputs on both surfaces. Retain no credentials in parameter files.
