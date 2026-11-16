# resources-deployments Specification

## Purpose
TBD - created by syncing change resources-deployments-tenant-scope. Update Purpose after archive.
## Requirements
### Requirement: Deployment template source
`deployment tenant create`, `deployment tenant validate`, `deployment tenant what-if`, `New-AzTenantDeployment`, `Test-AzTenantDeployment` and `Get-AzTenantDeploymentWhatIfResult` SHALL put the template given by TemplateFile, TemplateUri or TemplateSpecId into every request body they send. They SHALL send no ARM request when the template file does not exist.

#### Scenario: Local JSON template file is inlined
- **WHEN** `deployment tenant create`, `deployment tenant validate`, `deployment tenant what-if`, `New-AzTenantDeployment`, `Test-AzTenantDeployment` or `Get-AzTenantDeploymentWhatIfResult` runs with TemplateFile naming a local JSON file, which may contain comments and trailing commas
- **THEN** every request body the command sends SHALL carry the file's JSON content as `properties.template`

#### Scenario: Bicep template file is compiled
- **WHEN** `deployment tenant create`, `deployment tenant validate`, `deployment tenant what-if`, `New-AzTenantDeployment`, `Test-AzTenantDeployment` or `Get-AzTenantDeploymentWhatIfResult` runs with TemplateFile ending in `.bicep`
- **THEN** Azclips SHALL compile the file with the Bicep CLI
- **AND** every request body the command sends SHALL carry the compiled ARM JSON as `properties.template`

#### Scenario: Missing template file
- **WHEN** `deployment tenant create`, `deployment tenant validate`, `deployment tenant what-if`, `New-AzTenantDeployment`, `Test-AzTenantDeployment` or `Get-AzTenantDeploymentWhatIfResult` runs with TemplateFile naming a file that does not exist
- **THEN** Azclips SHALL raise a file-not-found error
- **AND** Azclips SHALL send no ARM request

#### Scenario: Template URI is linked
- **WHEN** `deployment tenant create`, `deployment tenant validate`, `deployment tenant what-if`, `New-AzTenantDeployment`, `Test-AzTenantDeployment` or `Get-AzTenantDeploymentWhatIfResult` runs with TemplateUri
- **THEN** every request body the command sends SHALL carry TemplateUri as `properties.templateLink.uri`
- **AND** the body SHALL carry no `properties.template`

#### Scenario: Query string is normalized onto the template link
- **WHEN** `deployment tenant create`, `deployment tenant validate`, `deployment tenant what-if`, `New-AzTenantDeployment`, `Test-AzTenantDeployment` or `Get-AzTenantDeploymentWhatIfResult` runs with TemplateUri and QueryString
- **THEN** every request body the command sends SHALL carry QueryString as `properties.templateLink.queryString`, with surrounding whitespace and one leading `?` or `&` removed

#### Scenario: Template Spec id is linked without a query string
- **WHEN** `deployment tenant create`, `deployment tenant validate`, `deployment tenant what-if`, `New-AzTenantDeployment`, `Test-AzTenantDeployment` or `Get-AzTenantDeploymentWhatIfResult` runs with TemplateSpecId
- **THEN** every request body the command sends SHALL carry TemplateSpecId as `properties.templateLink.id`
- **AND** `properties.templateLink` SHALL carry no `queryString`

### Requirement: Deployment parameter sources
`deployment tenant create`, `deployment tenant validate`, `deployment tenant what-if`, `New-AzTenantDeployment`, `Test-AzTenantDeployment` and `Get-AzTenantDeploymentWhatIfResult` SHALL build the request body's parameters from ParametersFile or ParametersUri and SHALL merge any Parameter values over them in order. They SHALL send no ARM request when the parameters file does not exist.

#### Scenario: JSON parameters file with a parameters wrapper
- **WHEN** `deployment tenant create`, `deployment tenant validate`, `deployment tenant what-if`, `New-AzTenantDeployment`, `Test-AzTenantDeployment` or `Get-AzTenantDeploymentWhatIfResult` runs with ParametersFile naming a JSON parameters file, which may contain comments, whose root has a `parameters` object
- **THEN** every request body the command sends SHALL carry that inner `parameters` object as `properties.parameters`

#### Scenario: JSON parameters file without a parameters wrapper
- **WHEN** `deployment tenant create`, `deployment tenant validate`, `deployment tenant what-if`, `New-AzTenantDeployment`, `Test-AzTenantDeployment` or `Get-AzTenantDeploymentWhatIfResult` runs with ParametersFile naming a JSON file whose root has no `parameters` object
- **THEN** every request body the command sends SHALL carry the whole root object as `properties.parameters`

#### Scenario: Bicep parameters file is compiled
- **WHEN** `deployment tenant create`, `deployment tenant validate`, `deployment tenant what-if`, `New-AzTenantDeployment`, `Test-AzTenantDeployment` or `Get-AzTenantDeploymentWhatIfResult` runs with ParametersFile ending in `.bicepparam`
- **THEN** Azclips SHALL compile the file with `bicep build-params`
- **AND** every request body the command sends SHALL carry the compiled parameters as `properties.parameters`

#### Scenario: Parameters URI is linked
- **WHEN** `deployment tenant create`, `deployment tenant validate`, `deployment tenant what-if`, `New-AzTenantDeployment`, `Test-AzTenantDeployment` or `Get-AzTenantDeploymentWhatIfResult` runs with ParametersUri
- **THEN** every request body the command sends SHALL carry ParametersUri as `properties.parametersLink.uri`
- **AND** the body SHALL carry no `properties.parameters`

#### Scenario: Parameter values merge in order over the file
- **WHEN** `deployment tenant create`, `deployment tenant validate`, `deployment tenant what-if`, `New-AzTenantDeployment`, `Test-AzTenantDeployment` or `Get-AzTenantDeploymentWhatIfResult` runs with one or more Parameter values, with or without ParametersFile
- **THEN** Azclips SHALL apply the values in the order given over the file's parameters
- **AND** parameter names SHALL match without regard to case
- **AND** for a name given more than once, the last value SHALL win

#### Scenario: KEY=VALUE parameter value
- **WHEN** `deployment tenant create`, `deployment tenant validate`, `deployment tenant what-if`, `New-AzTenantDeployment`, `Test-AzTenantDeployment` or `Get-AzTenantDeploymentWhatIfResult` runs with a Parameter value of the form `KEY=VALUE`
- **THEN** `properties.parameters` SHALL carry `KEY` with `{ "value": VALUE }`, where VALUE is read as JSON when it parses as JSON and as a string otherwise

#### Scenario: Raw JSON object parameter value
- **WHEN** `deployment tenant create`, `deployment tenant validate`, `deployment tenant what-if`, `New-AzTenantDeployment`, `Test-AzTenantDeployment` or `Get-AzTenantDeploymentWhatIfResult` runs with a Parameter value that is a raw JSON object of parameters
- **THEN** each parameter in that object SHALL be merged into `properties.parameters`

#### Scenario: Parameters file path as a parameter value
- **WHEN** `deployment tenant create`, `deployment tenant validate`, `deployment tenant what-if`, `New-AzTenantDeployment`, `Test-AzTenantDeployment` or `Get-AzTenantDeploymentWhatIfResult` runs with a Parameter value that is a path to a JSON parameters file, with or without a leading `@`
- **THEN** each parameter in that file SHALL be merged into `properties.parameters`

#### Scenario: Missing parameters file
- **WHEN** `deployment tenant create`, `deployment tenant validate`, `deployment tenant what-if`, `New-AzTenantDeployment`, `Test-AzTenantDeployment` or `Get-AzTenantDeploymentWhatIfResult` runs with ParametersFile naming a file that does not exist
- **THEN** Azclips SHALL raise a file-not-found error
- **AND** Azclips SHALL send no ARM request

#### Scenario: No parameter source
- **WHEN** `deployment tenant create`, `deployment tenant validate`, `deployment tenant what-if`, `New-AzTenantDeployment`, `Test-AzTenantDeployment` or `Get-AzTenantDeploymentWhatIfResult` runs with no ParametersFile, no ParametersUri and no Parameter value, for a template that needs no parameter values
- **THEN** every request body the command sends SHALL carry neither `properties.parameters` nor `properties.parametersLink`

### Requirement: Tenant deployment location and Incremental mode
`deployment tenant create`, `deployment tenant validate`, `deployment tenant what-if`, `New-AzTenantDeployment`, `Test-AzTenantDeployment` and `Get-AzTenantDeploymentWhatIfResult` SHALL send Location as the body's `location` and SHALL always deploy in `Incremental` mode, with no Mode input.

#### Scenario: Location is sent in the body
- **WHEN** `deployment tenant create`, `deployment tenant validate`, `deployment tenant what-if`, `New-AzTenantDeployment`, `Test-AzTenantDeployment` or `Get-AzTenantDeploymentWhatIfResult` runs with Location
- **THEN** every request body the command sends SHALL carry Location as `location`

#### Scenario: Mode is always Incremental
- **WHEN** `deployment tenant create`, `deployment tenant validate`, `deployment tenant what-if`, `New-AzTenantDeployment`, `Test-AzTenantDeployment` or `Get-AzTenantDeploymentWhatIfResult` runs
- **THEN** every request body the command sends SHALL carry `properties.mode` = `Incremental`

#### Scenario: No Mode input
- **WHEN** the inputs of `deployment tenant create`, `deployment tenant validate`, `deployment tenant what-if`, `New-AzTenantDeployment`, `Test-AzTenantDeployment` or `Get-AzTenantDeploymentWhatIfResult` are listed
- **THEN** none of them SHALL be a Mode input

### Requirement: Optional deployment name defaults to a GUID
`deployment tenant create`, `deployment tenant validate`, `deployment tenant what-if`, `New-AzTenantDeployment`, `Test-AzTenantDeployment` and `Get-AzTenantDeploymentWhatIfResult` SHALL accept an omitted Name and SHALL then use one new GUID as the deployment name for the whole command.

#### Scenario: Omitted name becomes one GUID for every request
- **WHEN** `deployment tenant create`, `deployment tenant validate`, `deployment tenant what-if`, `New-AzTenantDeployment`, `Test-AzTenantDeployment` or `Get-AzTenantDeploymentWhatIfResult` runs without Name
- **THEN** Azclips SHALL generate one new GUID
- **AND** every request the command sends SHALL use that GUID as `{deploymentName}`

### Requirement: Create validates, then deploys and waits
`deployment tenant create` and `New-AzTenantDeployment` SHALL validate the deployment before submitting it, and SHALL send the PUT only when validation succeeds. Unless `deployment tenant create` is given `--no-wait` or `New-AzTenantDeployment` is given `-NoWait`, they SHALL then wait for the deployment to finish and output the final deployment.

#### Scenario: Validation runs first with the PUT body
- **WHEN** `deployment tenant create` or `New-AzTenantDeployment` runs
- **THEN** Azclips SHALL first send `POST /providers/Microsoft.Resources/deployments/{deploymentName}/validate?api-version=2025-04-01` with the same body as the PUT

#### Scenario: A validation error stops the create
- **WHEN** the validate response of `deployment tenant create` or `New-AzTenantDeployment`, with HTTP 200 or 400, contains an `error`
- **THEN** Azclips SHALL raise an error whose first line is `Deployment validation failed.`, followed by one `code: message` line per error and nested error, each indented one level per nesting depth and ending in ` (target: <target>)` when the error has a target
- **AND** Azclips SHALL send no PUT

#### Scenario: Another validate failure stops the create
- **WHEN** the validate request of `deployment tenant create` or `New-AzTenantDeployment` fails with another status and no validation result
- **THEN** Azclips SHALL raise the ARM error
- **AND** Azclips SHALL send no PUT

#### Scenario: Successful create waits and outputs the deployment
- **WHEN** validation succeeds for `deployment tenant create` without `--no-wait`, or for `New-AzTenantDeployment` without `-NoWait`
- **THEN** Azclips SHALL send `PUT /providers/Microsoft.Resources/deployments/{deploymentName}?api-version=2025-04-01`
- **AND** Azclips SHALL wait for the operation to finish
- **AND** Azclips SHALL output the final `DeploymentExtended`

#### Scenario: Failed deployment raises an error
- **WHEN** the deployment that `deployment tenant create` without `--no-wait`, or `New-AzTenantDeployment` without `-NoWait`, is waiting on ends `Failed`
- **THEN** Azclips SHALL raise an error saying the long-running operation failed

#### Scenario: Canceled deployment outputs the final deployment
- **WHEN** the deployment that `deployment tenant create` without `--no-wait`, or `New-AzTenantDeployment` without `-NoWait`, is waiting on ends `Canceled`
- **THEN** Azclips SHALL output the final `DeploymentExtended`

### Requirement: Create without waiting
`deployment tenant create` SHALL accept `--no-wait`, and `New-AzTenantDeployment` SHALL accept `-NoWait` with the same behavior. With either, the command SHALL wait for the validate request to succeed and for the PUT submission to complete. It SHALL then output the PUT's initial response without waiting for the deployment to finish. Without either, both commands SHALL stay synchronous. `-NoWait` SHALL create no PowerShell Job.

#### Scenario: No-wait still validates first
- **WHEN** `deployment tenant create --no-wait` or `New-AzTenantDeployment -NoWait` runs
- **THEN** Azclips SHALL wait for the validate request to succeed
- **AND** every validation-failure scenario of the requirement "Create validates, then deploys and waits" SHALL apply unchanged, with no PUT on a validation error

#### Scenario: No-wait waits for the PUT submission
- **WHEN** validation succeeds for `deployment tenant create --no-wait` or `New-AzTenantDeployment -NoWait`
- **THEN** Azclips SHALL send `PUT /providers/Microsoft.Resources/deployments/{deploymentName}?api-version=2025-04-01`
- **AND** Azclips SHALL wait for the PUT submission to complete

#### Scenario: Failed PUT submission raises the ARM error
- **WHEN** the PUT submission of `deployment tenant create --no-wait` or `New-AzTenantDeployment -NoWait` fails
- **THEN** Azclips SHALL raise the ARM error

#### Scenario: Successful PUT submission outputs the initial response
- **WHEN** the PUT submission of `deployment tenant create --no-wait` or `New-AzTenantDeployment -NoWait` succeeds
- **THEN** Azclips SHALL output the PUT's initial response (`DeploymentExtended`)
- **AND** Azclips SHALL not wait for the deployment to finish

#### Scenario: Create stays synchronous by default
- **WHEN** `deployment tenant create` runs without `--no-wait`, or `New-AzTenantDeployment` runs without `-NoWait`
- **THEN** Azclips SHALL wait for the deployment to finish exactly as the requirement "Create validates, then deploys and waits" describes

#### Scenario: NoWait creates no PowerShell Job
- **WHEN** `New-AzTenantDeployment -NoWait` runs
- **THEN** Azclips SHALL return the initial deployment directly
- **AND** Azclips SHALL create no PowerShell Job

### Requirement: Validate a deployment
`deployment tenant validate` and `Test-AzTenantDeployment` SHALL validate the deployment without submitting it. They SHALL raise a readable error when the validation result contains an error, and otherwise output the validation result.

#### Scenario: Validate request
- **WHEN** `deployment tenant validate` or `Test-AzTenantDeployment` runs
- **THEN** Azclips SHALL send `POST /providers/Microsoft.Resources/deployments/{deploymentName}/validate?api-version=2025-04-01`

#### Scenario: Accepted validation is polled to completion
- **WHEN** ARM answers the validate request of `deployment tenant validate` or `Test-AzTenantDeployment` with 202
- **THEN** Azclips SHALL keep checking the `Location` header URI until the operation finishes

#### Scenario: A validation error is raised
- **WHEN** the validate result of `deployment tenant validate` or `Test-AzTenantDeployment`, with HTTP 200 or 400, contains an `error`
- **THEN** Azclips SHALL raise an error whose first line is `Deployment validation failed.`, followed by one `code: message` line per error and nested error, each indented one level per nesting depth and ending in ` (target: <target>)` when the error has a target

#### Scenario: Another validate failure raises the ARM error
- **WHEN** the validate request of `deployment tenant validate` or `Test-AzTenantDeployment` fails with another status and no validation result
- **THEN** Azclips SHALL raise the ARM error

#### Scenario: Successful validation outputs the result
- **WHEN** validation succeeds for `deployment tenant validate` or `Test-AzTenantDeployment`
- **THEN** Azclips SHALL output the `DeploymentValidateResult`

### Requirement: What-if a deployment
`deployment tenant what-if` and `Get-AzTenantDeploymentWhatIfResult` SHALL take the same inputs. Each SHALL send a what-if request with a normalized result format and poll it until ARM stops answering 202. Each SHALL raise a readable error when the result failed, and otherwise output the raw result.

#### Scenario: What-if inputs are the same on both surfaces
- **WHEN** `deployment tenant what-if` or `Get-AzTenantDeploymentWhatIfResult` runs
- **THEN** it SHALL accept the same inputs: Location (required), Name (optional, with the GUID default), TemplateFile, TemplateUri, TemplateSpecId, QueryString, ParametersFile, ParametersUri, Parameter, ValidationLevel and ResultFormat

#### Scenario: What-if request and polling
- **WHEN** `deployment tenant what-if` or `Get-AzTenantDeploymentWhatIfResult` runs
- **THEN** Azclips SHALL send `POST /providers/Microsoft.Resources/deployments/{deploymentName}/whatIf?api-version=2025-04-01`. The body SHALL carry `location`, the template and parameter sources, `properties.mode` = `Incremental`, `properties.validationLevel` when ValidationLevel is given, and `properties.whatIfSettings.resultFormat`
- **AND** Azclips SHALL keep checking the `Location` header URI until ARM stops answering 202

#### Scenario: Result format defaults to full resource payloads
- **WHEN** `deployment tenant what-if` or `Get-AzTenantDeploymentWhatIfResult` runs without ResultFormat
- **THEN** the body SHALL carry `properties.whatIfSettings.resultFormat` = `FullResourcePayloads`

#### Scenario: Result format is matched without regard to case
- **WHEN** `deployment tenant what-if` or `Get-AzTenantDeploymentWhatIfResult` runs with ResultFormat `ResourceIdOnly` or `FullResourcePayloads` in any letter case
- **THEN** the body SHALL carry the correctly capitalized value as `properties.whatIfSettings.resultFormat`

#### Scenario: A failed what-if is raised
- **WHEN** the what-if result of `deployment tenant what-if` or `Get-AzTenantDeploymentWhatIfResult` has `status: Failed` or contains an `error`
- **THEN** Azclips SHALL raise an error whose first line is `What-if operation failed.`, followed by one `code: message` line per error and nested error, each indented one level per nesting depth and ending in ` (target: <target>)` when the error has a target

#### Scenario: Successful what-if outputs the raw result
- **WHEN** the what-if of `deployment tenant what-if` or `Get-AzTenantDeploymentWhatIfResult` succeeds
- **THEN** Azclips SHALL output the raw `WhatIfOperationResult`

### Requirement: List tenant deployments
`deployment tenant list` and `Get-AzTenantDeployment_List` SHALL list every deployment at the tenant scope across all pages. They SHALL send `$filter`, URL-escaped, only when a filter is given, and SHALL never send `$top`.

#### Scenario: List request
- **WHEN** `deployment tenant list` runs, or `Get-AzTenantDeployment` runs without `-Name` (`Get-AzTenantDeployment_List`)
- **THEN** Azclips SHALL send `GET /providers/Microsoft.Resources/deployments?api-version=2025-04-01`

#### Scenario: Filter is sent URL-escaped
- **WHEN** `deployment tenant list --filter <expr>` or `Get-AzTenantDeployment -Filter <expr>` (`Get-AzTenantDeployment_List`) runs
- **THEN** Azclips SHALL send `GET /providers/Microsoft.Resources/deployments?api-version=2025-04-01&$filter=<expr>`, with `<expr>` URL-escaped
- **AND** Azclips SHALL follow every `nextLink`
- **AND** Azclips SHALL output each deployment

#### Scenario: Every page is read
- **WHEN** a page returned to `deployment tenant list` or `Get-AzTenantDeployment_List` has a `nextLink`
- **THEN** Azclips SHALL follow every `nextLink` until a page has none
- **AND** Azclips SHALL output each deployment of every page

#### Scenario: No filter sends no $filter
- **WHEN** `deployment tenant list` or `Get-AzTenantDeployment_List` runs without a filter
- **THEN** the request SHALL carry no `$filter` query parameter

#### Scenario: $top is never sent
- **WHEN** `deployment tenant list` or `Get-AzTenantDeployment_List` runs
- **THEN** no request SHALL carry a `$top` query parameter

### Requirement: Show a deployment by name
`deployment tenant show` and `Get-AzTenantDeployment_Get` SHALL get the named tenant deployment and output it.

#### Scenario: Show by name
- **WHEN** `deployment tenant show --name <name>` or `Get-AzTenantDeployment -Name <name>` (`Get-AzTenantDeployment_Get`) runs
- **THEN** Azclips SHALL send `GET /providers/Microsoft.Resources/deployments/<name>?api-version=2025-04-01`
- **AND** Azclips SHALL output the `DeploymentExtended`

#### Scenario: Unknown deployment
- **WHEN** `deployment tenant show` or `Get-AzTenantDeployment_Get` names a deployment that does not exist
- **THEN** Azclips SHALL raise ARM's not-found error

### Requirement: Delete a deployment by name
`deployment tenant delete` and `Remove-AzTenantDeployment` SHALL delete the named tenant deployment, wait for the delete to finish, and output nothing.

#### Scenario: Delete by name waits and outputs nothing
- **WHEN** `deployment tenant delete --name <name>` or `Remove-AzTenantDeployment -Name <name>` runs
- **THEN** Azclips SHALL send `DELETE /providers/Microsoft.Resources/deployments/<name>?api-version=2025-04-01`
- **AND** Azclips SHALL wait for the operation to finish
- **AND** Azclips SHALL output nothing

### Requirement: Cancel a deployment by name
`deployment tenant cancel` and `Stop-AzTenantDeployment` SHALL cancel the named tenant deployment and output nothing. They SHALL raise ARM's error when ARM rejects the cancel.

#### Scenario: Cancel by name outputs nothing
- **WHEN** `deployment tenant cancel --name <name>` or `Stop-AzTenantDeployment -Name <name>` runs
- **THEN** Azclips SHALL send `POST /providers/Microsoft.Resources/deployments/<name>/cancel?api-version=2025-04-01`
- **AND** Azclips SHALL output nothing

#### Scenario: Rejected cancel raises the ARM error
- **WHEN** ARM rejects the cancel sent by `deployment tenant cancel` or `Stop-AzTenantDeployment`, for example because the deployment is no longer running
- **THEN** Azclips SHALL raise ARM's error

### Requirement: Export only the deployment template
`deployment tenant export` SHALL export the named tenant deployment's template and output only the template value. It SHALL raise an error when the response has no template.

#### Scenario: Export outputs the template value only
- **WHEN** `deployment tenant export --name <name>` runs
- **THEN** Azclips SHALL send `POST /providers/Microsoft.Resources/deployments/<name>/exportTemplate?api-version=2025-04-01`
- **AND** Azclips SHALL output only the `template` value, not the `{ "template": … }` wrapper

#### Scenario: Export without a template raises an error
- **WHEN** the export response of `deployment tenant export --name <name>` has no `template`
- **THEN** Azclips SHALL raise `The export response for deployment '<name>' at tenant scope did not contain a template.`
- **AND** Azclips SHALL output nothing

### Requirement: Save a deployment template to a file
`Save-AzTenantDeploymentTemplate` SHALL export the named tenant deployment's template and write it as indented JSON to a file. It SHALL not overwrite an existing file unless `-Force` is set, and SHALL output the full path of the written file.

#### Scenario: Save sends the export request
- **WHEN** `Save-AzTenantDeploymentTemplate -DeploymentName <name>` runs
- **THEN** Azclips SHALL send `POST /providers/Microsoft.Resources/deployments/<name>/exportTemplate?api-version=2025-04-01`

#### Scenario: Default path is the current PowerShell location
- **WHEN** `Save-AzTenantDeploymentTemplate -DeploymentName <name>` runs without `-Path`
- **THEN** Azclips SHALL write `<current PowerShell location>/<name>.json`

#### Scenario: Path that is an existing folder
- **WHEN** `Save-AzTenantDeploymentTemplate -DeploymentName <name>` runs with `-Path` naming an existing folder
- **THEN** Azclips SHALL write `<Path>/<name>.json`

#### Scenario: Path that is not an existing folder
- **WHEN** `Save-AzTenantDeploymentTemplate -DeploymentName <name>` runs with `-Path` that does not name an existing folder
- **THEN** Azclips SHALL write to `-Path`, adding `.json` when the path does not already end with it
- **AND** Azclips SHALL create any missing parent folder

#### Scenario: Existing file without -Force
- **WHEN** the target file of `Save-AzTenantDeploymentTemplate` already exists and `-Force` is not set
- **THEN** Azclips SHALL raise `File '<path>' already exists. Use -Force to overwrite it.`
- **AND** Azclips SHALL write nothing

#### Scenario: Written file and output
- **WHEN** `Save-AzTenantDeploymentTemplate` writes the file
- **THEN** the file SHALL hold the indented template JSON
- **AND** Azclips SHALL output an object whose `Path` is the full file path

#### Scenario: Export response without a template
- **WHEN** the export response of `Save-AzTenantDeploymentTemplate` has no template
- **THEN** Azclips SHALL raise an error saying the response has no template
- **AND** Azclips SHALL write nothing

### Requirement: Wait for a deployment condition
`deployment tenant wait` SHALL poll the named tenant deployment until the requested condition holds or the timeout elapses. The condition is `--created`, `--updated`, `--deleted`, `--exists` or a `--custom` JMESPath expression. The command SHALL output nothing when the condition holds.

#### Scenario: A condition is required
- **WHEN** `deployment tenant wait --name <name>` runs without any of `--created`, `--updated`, `--deleted`, `--exists` or `--custom`
- **THEN** Azclips SHALL raise `incorrect usage: --created | --updated | --deleted | --exists | --custom JMESPATH`
- **AND** Azclips SHALL send no request

#### Scenario: Invalid custom expression
- **WHEN** `deployment tenant wait --custom <expression>` runs with an expression that is not valid JMESPath
- **THEN** Azclips SHALL raise an invalid-input error naming the expression
- **AND** Azclips SHALL send no request

#### Scenario: Polling interval and timeout
- **WHEN** `deployment tenant wait --name <name>` runs with `--created`, `--updated`, `--deleted`, `--exists` or `--custom`
- **THEN** Azclips SHALL send `GET /providers/Microsoft.Resources/deployments/<name>?api-version=2025-04-01` every `--interval` seconds, 30 by default, until the condition holds or `--timeout` seconds, 3600 by default, elapse

#### Scenario: Wait for exists
- **WHEN** `deployment tenant wait --exists` runs
- **THEN** Azclips SHALL stop on the first successful GET, whatever `provisioningState` is, including `Failed`
- **AND** the `Failed` check SHALL not apply

#### Scenario: Wait for created or updated
- **WHEN** `deployment tenant wait --created` or `deployment tenant wait --updated` runs
- **THEN** Azclips SHALL stop once `properties.provisioningState` is `Succeeded`, in any letter case

#### Scenario: Wait for deleted
- **WHEN** `deployment tenant wait --deleted` runs
- **THEN** Azclips SHALL stop once the GET returns 404

#### Scenario: Wait for a custom condition
- **WHEN** `deployment tenant wait --custom <JMESPath>` runs
- **THEN** Azclips SHALL evaluate the expression against each successful GET response body, the deployment's ARM JSON
- **AND** Azclips SHALL stop once the result is truthy under JMESPath truthiness: not null, not false, and not an empty string, array or object

#### Scenario: Custom condition reaches Canceled
- **WHEN** `deployment tenant wait --custom "properties.provisioningState=='Canceled'"` runs and the deployment reaches `Canceled`
- **THEN** Azclips SHALL stop
- **AND** Azclips SHALL output nothing

#### Scenario: Not found while waiting
- **WHEN** a GET sent by `deployment tenant wait` returns 404
- **THEN** with `--created` or `--custom`, Azclips SHALL keep waiting
- **AND** with `--deleted`, Azclips SHALL stop
- **AND** with `--updated` alone, Azclips SHALL raise ARM's not-found error

#### Scenario: Failed deployment stops the wait
- **WHEN** a GET sent by `deployment tenant wait` without `--exists` returns `provisioningState` `Failed`
- **THEN** Azclips SHALL raise `The operation failed`, also when `--custom` is given

#### Scenario: Other ARM errors are raised
- **WHEN** a GET sent by `deployment tenant wait` fails with any other ARM error
- **THEN** Azclips SHALL raise that ARM error

#### Scenario: Condition holds
- **WHEN** the condition requested from `deployment tenant wait` holds
- **THEN** Azclips SHALL output nothing

#### Scenario: Timeout elapses
- **WHEN** `--timeout` seconds elapse before the condition requested from `deployment tenant wait` holds
- **THEN** Azclips SHALL raise `Wait operation timed-out after <timeout> seconds`
