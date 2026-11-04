## Why

The active change `resources-deployments-tenant-scope` adds the tenant-scope deployment commands to the Resources extension, but no record/playback scenario pins their command-to-REST translation or their output. Every CLI and PowerShell tenant deployment command needs a scenario that replays on both surfaces in CI without credentials.

## What Changes

- Add two independent scenario test cases. Each converts one upstream azure-cli tenant test and has its own recording. Both record against real Azure in the `DeploymentOperationScenarioTests` style: record mode runs the CLI surface with live auth, and playback replays the committed recording on both surfaces.
  - **T1** Tenant deployment lifecycle, from `DeploymentTestAtTenantScope.test_tenant_level_deployment`. It runs validate, create, list with and without a filter, show, export (CLI) or save (PowerShell), and delete. Then a second deployment is created without waiting, cancelled, waited on (CLI) or polled (PowerShell) until `Canceled`, and shown.
  - **T2** Tenant deployment what-if with the default result format, from `DeploymentWhatIfAtTenantScopeTest.test_tenant_level_what_if`.
- Setup and cleanup use only first-class Azclips commands. T1 cleans up with `deployment tenant delete` and `group delete`. T2 creates nothing and needs no cleanup. The scenarios never use `rest` or `Invoke-AzRestMethod`.
- Not converted, because the user kept only the two upstream azure-cli tenant tests:
  - The validation-level case (azure-cli `test_tenant_deployment_with_validation_level`, azure-powershell `Test-WhatIfProviderNoRbacAtTenantScope`).
  - The omitted-name generated-GUID case.
  - The `ResourceIdOnly` what-if variation (azure-powershell `Test-WhatIfWithResourceIdOnlyAtTenantScope`).
  - The Template Spec case. It is also `prerequisite-missing`: Azclips has no command that creates a Template Spec, and `rest` is not allowed.
  - The Bicep case. Its playback also breaks: CI has no Bicep CLI, and the compiled template embeds the Bicep version.

## Capabilities

### New Capabilities
- `resources-deployments-tests`: scenario tests that verify the `resources-deployments` behavior requirements for the tenant-scope deployment commands:
  - CLI: `deployment tenant create|validate|what-if|list|show|delete|cancel|export|wait`
  - PowerShell: `New-AzTenantDeployment`, `Test-AzTenantDeployment`, `Get-AzTenantDeploymentWhatIfResult`, `Get-AzTenantDeployment_List`, `Get-AzTenantDeployment_Get`, `Stop-AzTenantDeployment`, `Remove-AzTenantDeployment`, `Save-AzTenantDeploymentTemplate`

### Modified Capabilities
(none) `resources-deployments` belongs to the active change `resources-deployments-tenant-scope`, and its requirements do not change here.

## Impact

- New scenario test file: `tests/Scenario.Tests/Resources/TenantDeploymentScenarioTests.cs`, with two independent test methods.
- New recordings, written by record mode only after user approval, one per method, under `tests/Scenario.Tests/Resources/Records/TenantDeploymentScenarioTests/`.
- Harness: none. `ExtensionUnderTest.ResourcesDeployments` already routes every `deployment` command through its root leaf, `ExtensionUnderTest.Resources` routes `group`, and `Scenario.Tests.csproj` already references `Extensions.Resources.csproj`.
- Recording T1 creates two tenant-root deployments and one resource group in the recorder's subscription, then deletes them. T2 creates nothing.
- The recorder needs these deployment permissions at tenant root (`/`):
  - `Microsoft.Resources/deployments/write` for T1.
  - The what-if action for T2.
  - T1 also needs permission to create resource groups in the subscription.
- No product code, OpenSpec behavior spec or docs change.
