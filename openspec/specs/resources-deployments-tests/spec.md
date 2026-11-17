# resources-deployments-tests Specification

## Purpose
Record/playback scenario tests for the Azclips tenant-scope deployment commands: the CLI `deployment tenant ...` commands and the PowerShell `*-AzTenantDeployment*` cmdlets. Each test replays its committed recording on both surfaces and verifies the behavior stated in the `resources-deployments` spec.
## Requirements
### Requirement: Create validates, then deploys and waits
The scenario tests SHALL verify the behavior requirement "Create validates, then deploys and waits".

#### Scenario: T1 Tenant deployment lifecycle: validate, create, list, show, export, delete, then cancel and wait on a second deployment
- **WHEN** (CLI) step 1 runs `deployment tenant validate --name <deployment> --location <location> --template-file <tenant-template> --parameter nestedSubId=<subscription> --parameter nestedRG=<rg>`
- **THEN** outputs one `DeploymentValidateResult` whose `error` is null
- **AND** the result's `properties.provisioningState` is `Succeeded`
- **AND** the request is `POST /providers/Microsoft.Resources/deployments/<deployment>/validate?api-version=2025-04-01`
- **AND** the request body's `location` is `<location>` and `properties.mode` is `Incremental`
- **AND** the request body's `properties.template` equals the JSON content of `<tenant-template>` with its trailing `//` comment dropped, and the body has no `properties.templateLink`
- **AND** the request body's `properties.parameters` is `{"nestedSubId":{"value":"<subscription>"},"nestedRG":{"value":"<rg>"}}` and the body has no `properties.parametersLink`
- **WHEN** (CLI) step 2 runs `deployment tenant create --name <deployment> --location <location> --template-file <tenant-template> --parameter nestedSubId=<subscription> --parameter nestedRG=<rg>`
- **THEN** outputs one `DeploymentExtended` whose `name` is `<deployment>`; `properties.provisioningState` `Succeeded`; `properties.mode` `Incremental`
- **AND** the command sends `POST .../<deployment>/validate?api-version=2025-04-01` before `PUT .../<deployment>?api-version=2025-04-01`, and the validate and PUT request bodies are identical
- **WHEN** (CLI) step 3 runs `deployment tenant list`
- **THEN** outputs one `List<DeploymentExtended>` holding exactly one deployment named `<deployment>` (after trimming to this scenario)
- **AND** the request is `GET /providers/Microsoft.Resources/deployments?api-version=2025-04-01` with no `$filter` and no `$top`
- **WHEN** (CLI) step 4 runs `deployment tenant list --filter "provisioningState eq 'Succeeded'"`
- **THEN** holds exactly one deployment named `<deployment>` (after trimming); the request carries `$filter` set to the URL-escaped expression and no `$top`
- **WHEN** (CLI) step 5 runs `deployment tenant show --name <deployment>`
- **THEN** one `DeploymentExtended` named `<deployment>`, `provisioningState` `Succeeded`
- **WHEN** (CLI) step 6 runs `deployment tenant export --name <deployment>`
- **THEN** outputs one JSON value that is the template itself: it has `$schema`, a `resources` array, and no top-level `template` property
- **AND** its `resources` array has 1 entry (the nested deployment)
- **AND** the request is `POST /providers/Microsoft.Resources/deployments/<deployment>/exportTemplate?api-version=2025-04-01`
- **WHEN** (CLI) step 7 runs `deployment tenant delete --name <deployment>`
- **THEN** outputs nothing; the request is `DELETE /providers/Microsoft.Resources/deployments/<deployment>?api-version=2025-04-01`
- **WHEN** (CLI) step 8 runs `deployment tenant create --name <deployment2> --location <location> --template-file <slow-tenant-template> --parameter nestedSubId=<subscription> --parameter nestedRG=<rg> --no-wait`
- **THEN** outputs one `DeploymentExtended` named `<deployment2>` whose `provisioningState` is `Accepted` or `Running`
- **AND** the command sends `POST .../<deployment2>/validate`, then `PUT .../<deployment2>`; the create itself polls nothing, so the next recorded request after that PUT is step 9's first GET
- **WHEN** (CLI) step 9 runs `deployment tenant wait --name <deployment2> --custom "properties.provisioningState=='Running'" --interval 1 --timeout 300`, so the cancel is sent only once the deployment is `Running` (ARM answers a cancel sent while the deployment is still `Accepted` with 204, but can drop it and let the deployment run to `Succeeded`)
- **THEN** outputs nothing; every request is `GET .../<deployment2>?api-version=2025-04-01` and only the last response has `provisioningState` `Running`
- **WHEN** (CLI) step 10 runs `deployment tenant cancel --name <deployment2>`
- **THEN** outputs nothing; the request is `POST .../<deployment2>/cancel?api-version=2025-04-01`
- **WHEN** (CLI) step 11 runs `deployment tenant show --name <deployment2>`
- **THEN** `provisioningState` is neither `Succeeded` nor `Failed` (still `Accepted`, `Running`, `Canceling` or `Canceled`); if it is `Succeeded` or `Failed`, the test fails immediately with a message asking for a re-record (the cancel race)
- **WHEN** (CLI) step 12 runs `deployment tenant wait --name <deployment2> --custom "properties.provisioningState=='Canceled'" --interval 1 --timeout 300`
- **THEN** outputs nothing; every request is `GET .../<deployment2>?api-version=2025-04-01` and only the last response has `provisioningState` `Canceled`
- **WHEN** (CLI) step 13 runs `deployment tenant show --name <deployment2>`
- **THEN** `provisioningState` is `Canceled`
- **WHEN** (PowerShell) step 1 runs `Test-AzTenantDeployment -Name <deployment> -Location <location> -TemplateFile <tenant-template> -Parameter "nestedSubId=<subscription>","nestedRG=<rg>"`
- **THEN** one `DeploymentValidateResult`, `error` null, `provisioningState` `Succeeded`
- **WHEN** (PowerShell) step 2 runs `New-AzTenantDeployment -Name <deployment> -Location <location> -TemplateFile <tenant-template> -Parameter "nestedSubId=<subscription>","nestedRG=<rg>"`
- **THEN** one `DeploymentExtended` named `<deployment>`, `provisioningState` `Succeeded`, `mode` `Incremental`
- **WHEN** (PowerShell) step 3 runs `Get-AzTenantDeployment_List`
- **THEN** exactly one deployment named `<deployment>` (after trimming)
- **WHEN** (PowerShell) step 4 runs `Get-AzTenantDeployment_List -Filter "provisioningState eq 'Succeeded'"`
- **THEN** exactly one deployment named `<deployment>` (after trimming)
- **WHEN** (PowerShell) step 5 runs `Get-AzTenantDeployment_Get -Name <deployment>`
- **THEN** one `DeploymentExtended` named `<deployment>`, `provisioningState` `Succeeded`
- **WHEN** (PowerShell) step 6 runs `Save-AzTenantDeploymentTemplate -DeploymentName <deployment> -Path <temp folder> -Force`
- **THEN** outputs one `DeploymentTemplateSaveResult` whose `Path` is `<temp folder>/<deployment>.json`; the file holds indented JSON of the template with `$schema` and a `resources` array (test deletes it afterwards)
- **AND** the file's `resources` array has 1 entry (the nested deployment)
- **WHEN** (PowerShell) step 7 runs `Remove-AzTenantDeployment -Name <deployment>`
- **THEN** outputs nothing
- **WHEN** (PowerShell) step 8 runs `New-AzTenantDeployment -Name <deployment2> -Location <location> -TemplateFile <slow-tenant-template> -Parameter "nestedSubId=<subscription>","nestedRG=<rg>" -NoWait`
- **THEN** one `DeploymentExtended` named `<deployment2>`, `provisioningState` `Accepted` or `Running`
- **WHEN** (PowerShell) step 9 runs `Get-AzTenantDeployment_Get -Name <deployment2>`, repeated until `provisioningState` is `Running` (replays the GETs CLI step 9 recorded)
- **THEN** the last output's `provisioningState` is `Running`
- **WHEN** (PowerShell) step 10 runs `Stop-AzTenantDeployment -Name <deployment2>`
- **THEN** outputs nothing
- **WHEN** (PowerShell) step 11 runs `Get-AzTenantDeployment_Get -Name <deployment2>` (consumes the GET CLI step 11 recorded)
- **THEN** `provisioningState` is neither `Succeeded` nor `Failed` (still `Accepted`, `Running`, `Canceling` or `Canceled`); if it is `Succeeded` or `Failed`, the test fails immediately with a message asking for a re-record (the cancel race)
- **WHEN** (PowerShell) step 12 runs `Get-AzTenantDeployment_Get -Name <deployment2>`, repeated until `provisioningState` is `Canceled` (replays the GETs CLI step 12 recorded)
- **THEN** the last output's `provisioningState` is `Canceled`
- **WHEN** (PowerShell) step 13 runs `Get-AzTenantDeployment_Get -Name <deployment2>`
- **THEN** `provisioningState` is `Canceled`

### Requirement: What-if a deployment
The scenario tests SHALL verify the behavior requirement "What-if a deployment".

#### Scenario: T2 Tenant deployment what-if with the default result format
- **WHEN** (CLI) step 1 runs `deployment tenant what-if --name <deployment> --location <location> --template-file <tenant-template> --parameter nestedSubId=<subscription> --parameter nestedRG=<rg>`
- **THEN** outputs one `WhatIfOperationResult` whose `status` is `Succeeded` and whose `error` is null
- **AND** `properties.changes` is not empty and each has `changeType` `Create`
- **AND** the request is `POST /providers/Microsoft.Resources/deployments/<deployment>/whatIf?api-version=2025-04-01`
- **AND** the request body's `properties.whatIfSettings.resultFormat` is `FullResourcePayloads` and `properties.mode` is `Incremental`
- **WHEN** (PowerShell) step 1 runs `Get-AzTenantDeploymentWhatIfResult -Name <deployment> -Location <location> -TemplateFile <tenant-template> -Parameter "nestedSubId=<subscription>","nestedRG=<rg>"`
- **THEN** one `WhatIfOperationResult`, `status` `Succeeded`, `error` null; changes not empty, each `changeType` `Create`
