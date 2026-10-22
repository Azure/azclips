# Scenario 7: Run repeatable deployments from a pipeline

**October 21 scope note:** OIDC is reported done; validate it on the intended pipeline/build. Cross-command scripting/stream consistency and LRO/idempotency contracts are deferred to GA, not Pending PP delivery. Retain basic failure detection, repeat-deployment observations and cleanup without treating universal contracts as PP gates.

**Customer goal:** "I want a pipeline to authenticate without a browser, deploy predictably and fail clearly when a command fails."

**Successful outcome:** A non-interactive job deploys the intended resource, a repeat run does not duplicate it, failures are observable and the job cleans up its test resources and local sign-in state.

## Prerequisites

- An isolated, ephemeral test agent with AzCLIPS CLI, preview PowerShell modules, PowerShell 7.6+ and the scenario 4 template/Bicep tooling.
- A preconfigured test service principal with least-privilege access to the test scope.
- For OIDC, an approved federation trust and a freshly issued token file from the pipeline provider.

## Shared inputs

Commands use PowerShell syntax inside the job. Choose one authentication variant and one deployment surface per run. Never echo credentials, tokens or the full process environment.

```powershell
$ErrorActionPreference = 'Stop'
$az = '<full-path-to-agent-AzCLIPS-executable>'
$tenant = '<test-tenant-id>'
$sub = '<test-subscription-id>'
$clientId = '<preconfigured-application-client-ID>'
$location = '<approved-region>'
$template = '<full-path-to-scenario-4-sample-storage.bicep>'
$run = Get-Date -Format 'yyyyMMddHHmmss'
$profile = "pipeline-$run"
$rg = "azclips-ci-$run"
$deployment = "ci-$run"
$storage = "clips$([guid]::NewGuid().ToString('N').Substring(0, 18))"
```

## Customer journey

| Step | Customer action | Commands (AzCLIPS CLI & AzCLIPS PowerShell) | Expected result / verification |
|---|---|---|---|
| **1. Authenticate the job** | Use a federated identity without interactive prompts. | **Pending live validation, source-backed OIDC commands:**<br>`$federatedToken = (Get-Content -LiteralPath $env:AZURE_FEDERATED_TOKEN_FILE -Raw).Trim()`<br><br>**AzCLIPS CLI:**<br>`& $az login --service-principal --client-id $clientId --tenant-id $tenant --subscription-id $sub --federated-token $federatedToken --out-profile $profile`<br><br>**AzCLIPS PowerShell:**<br>`Connect-AzAccount -ServicePrincipal -ClientId $clientId -TenantId $tenant -SubscriptionId $sub -FederatedToken $federatedToken -OutProfile $profile`<br><br>**Pending:** exact client-secret and certificate sign-in commands and secure credential-input mechanisms. | Login completes without browser/device-code prompts and selects the intended tenant/subscription. Provider masking is enabled and no token enters job logs. Use only an isolated agent: a CLI token argument may be visible in process arguments. Missing or expired tokens must fail the job. |
| **2. Prepare a repeatable deployment** | Inspect the saved context and create a uniquely named test group. | **AzCLIPS CLI:**<br>`& $az profile show $profile`<br>`& $az group create --subscription-id $sub --name $rg --location $location`<br><br>**AzCLIPS PowerShell:**<br>`Get-AzProfile -Name $profile`<br>`New-AzResourceGroup -SubscriptionId $sub -Name $rg -Location $location`<br><br>For CLI native calls, use the exit-code check below. | Job confirms its context before changes. Each run owns its group; reruns of a deployment within the job reuse the same deployment and storage names. |
| **3. Deploy twice and verify** | Run the same infrastructure deployment twice with the same inputs. | **Pending release confirmation, AzCLIPS CLI:**<br>`& $az deployment group create --subscription-id $sub --resource-group $rg --name $deployment --template-file $template --parameter "storageAccountName=$storage" --mode Incremental`<br><br>**Pending release confirmation, AzCLIPS PowerShell:**<br>`New-AzResourceGroupDeployment -SubscriptionId $sub -ResourceGroupName $rg -Name $deployment -TemplateFile $template -Parameter "storageAccountName=$storage" -Mode Incremental`<br><br>Repeat the chosen command.<br>**Pending:** terminal-state/resource verification, no-wait polling and cancellation commands. | Both deployments reach a successful terminal state without duplicate resources or prompts. Record the same resource ID after both runs. **Pending:** completion, cancellation and idempotency behavior still require Engineering confirmation. |
| **4. Verify automation failures** | Run a controlled missing-resource lookup and check failure detection and output streams. | **AzCLIPS CLI:**<br>`& $az group show --subscription-id $sub --name "$rg-missing" --output json`<br>`$failureCode = $LASTEXITCODE`<br>`if ($failureCode -eq 0) { throw 'Missing-resource lookup unexpectedly succeeded.' }`<br><br>**AzCLIPS PowerShell:**<br>`Get-AzResourceGroup -SubscriptionId $sub -Name "$rg-missing" -ErrorAction Stop`<br>Run in a dedicated negative-test task that expects the specific not-found error.<br><br>**Pending:** uniform exit-code and PowerShell stream contracts across commands. | CLI has a nonzero exit for expected not-found; PowerShell raises the expected error. Authentication or network failures are not passes. Verify CLI results remain machine-readable and diagnostics do not contaminate stdout; PowerShell success output remains structured. An unexpected failure must fail the deployment job. |
| **5. Always clean up** | Remove the group, sign out and remove the job profile, including after a failed deployment. | **AzCLIPS CLI:**<br>`& $az group delete --subscription-id $sub --name $rg`<br>`& $az group exists --subscription-id $sub --name $rg`<br>`& $az profile set --name $profile`<br>`& $az logout`<br>`& $az profile remove --name $profile`<br><br>**AzCLIPS PowerShell:**<br>`Remove-AzResourceGroup -SubscriptionId $sub -Name $rg -Force`<br>`Get-AzResourceGroup -SubscriptionId $sub -Name $rg`<br>`Set-AzProfile -Name $profile`<br>`Disconnect-AzAccount`<br>`Remove-AzProfile -Name $profile`<br><br>Clear `$federatedToken` and dispose of the ephemeral agent. | No run-owned resources or saved job profile remain. Cleanup failure is reported separately and does not hide the original deployment failure. Do not delete the preconfigured service principal or federation trust. |

## Native CLI failure handling

PowerShell's `$ErrorActionPreference` alone does not reliably turn a native exit code into a terminating error. Immediately after every CLI happy-path call:

```powershell
if ($LASTEXITCODE -ne 0) {
    throw "AzCLIPS command failed with exit code $LASTEXITCODE."
}
```

Configure the pipeline's cleanup task to run even when prior tasks fail. Run the expected-failure probe separately from production-style deployment steps.

## Authentication variants

- **Pending:** repeat with a client secret from the pipeline's secret store, never a literal in source or logs.
- **Pending:** repeat with an approved certificate and protected private-key access.
- On an approved identity-enabled Azure agent, use `& $az login --identity --client-id $clientId --subscription-id $sub --out-profile $profile` or `Connect-AzAccount -Identity -ClientId $clientId -SubscriptionId $sub -OutProfile $profile`, with `$clientId` set to the assigned managed identity's client ID rather than the service principal's ID.
