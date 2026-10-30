<!-- markdownlint-disable MD022 MD032 MD041 -->

<!--
    Please leave this section at the top of the change log.

    Changes for the upcoming release should go under the section titled "Upcoming Release", and should adhere to the following format:

    ## Upcoming Release
    * Overview of change #1
        - Additional information about change #1
    * Overview of change #2
        - Additional information about change #2

    ## Version X.Y.Z
    * Overview of change #1
        - Additional information about change #1
-->
## Upcoming Release
* Added subscription-scope deployment cmdlets `New-AzDeployment`, `Test-AzDeployment`, `Get-AzDeployment`, `Stop-AzDeployment`, and `Remove-AzDeployment`
    - Legacy `*-AzSubscriptionDeployment` names are accepted as aliases
    - `New-AzDeployment` and `Get-AzDeployment` output a `PSDeployment` object with the Az.Resources properties (`DeploymentName`, `ProvisioningState`, `Parameters`, `Outputs`, `ParametersString`, `OutputsString` and more)
    - `Stop-AzDeployment` and `Remove-AzDeployment` accept `-Id` or a `PSDeployment` piped from `Get-AzDeployment` (`-InputObject`), and `-PassThru` outputs `true` when the cancel or delete runs
    - `New-AzDeployment` reports the deployment's ARM error, up to 3 failed operation errors, and the correlation id when the deployment fails
    - `New-AzDeployment` validates the deployment before submitting it and throws `Deployment validation failed.` without creating a deployment when validation fails
    - `Test-AzDeployment` outputs nothing when validation passes, and outputs the validation errors as `PSResourceManagerError` objects (`Code`, `Message`, `Target`, `Details`) instead of failing when it fails
    - `Test-AzDeployment` writes the validation diagnostics as a warning unless the new `-SuppressDiagnostics` switch is set
* Added `Get-AzDeploymentWhatIfResult` (alias `Get-AzSubscriptionDeploymentWhatIfResult`) to preview a subscription-scope deployment with `-Location` and the template and parameter inputs of `New-AzDeployment`
    - `-ResultFormat` accepts `FullResourcePayloads` (default) or `ResourceIdOnly`
    - `-ExcludeChangeType` removes the listed change types from the result's change list
* Added `Save-AzDeploymentTemplate` (alias `Save-AzSubscriptionDeploymentTemplate`) to save a subscription-scope deployment's template to a JSON file
    - `-Path`, `-Force` and the output follow `Save-AzResourceGroupDeploymentTemplate`
* When `-Name` is omitted, `New-AzDeployment`, `Test-AzDeployment`, `Test-AzResourceGroupDeployment`, and `Test-AzManagementGroupDeployment` name the deployment after the template file or URI instead of a GUID
    - A GUID is still used when no template file or URI is given, or the derived name is not a valid deployment name
* Inline `-Parameter` `Name=Value` values for deployment cmdlets are typed by the template's declared parameter type when the template is local
    - A value that does not fit an `int`, `bool`, `object` or `array` parameter throws an error naming the parameter
* `New-AzDeployment` and `Test-AzDeployment` throw an error asking to upgrade Bicep CLI when a `.bicepparam` file is used with Bicep CLI older than 0.16.1, or with inline `-Parameter` values and Bicep CLI older than 0.22.6
    - No request is sent; the installed version is read once per invocation
* `New-AzDeployment` and `Test-AzDeployment` accept a `.bicepparam` path in `-Parameter` and use it as the parameters file, as if given as `-ParametersFile`
    - The other `-Parameter` values overlay it; combining it with `-ParametersFile` or another `.bicepparam` path throws an error
* Added `-Tag` to `New-AzDeployment` and `Test-AzDeployment` to tag the deployment
    - The tags are sent with both the validate request and the deployment request; no tags are sent when `-Tag` is omitted or empty
* Added `-DeploymentDebugLogLevel` to `New-AzDeployment` to log the request and/or response content of the deployment (`RequestContent`, `ResponseContent`, `All` or `None`)
    - The level is sent with both the validate request and the deployment request, with a warning that the logged content is readable from the deployment operations; an unknown level throws an error listing the allowed values
* Added `-TemplateParameterObject` to `New-AzDeployment`, `Test-AzDeployment` and `Get-AzDeploymentWhatIfResult` to supply template parameter values as a hashtable
    - Each entry is sent as the parameter's value; an entry holding a `reference` hashtable (a Key Vault reference) is sent as written, and `-Parameter` values override same-named entries
    - Combining it with `-ParametersFile` or `-ParametersUri` throws an invalid-input error before any request is sent

## Version 0.1.4
* Added management-group deployment create, get, stop, remove, and test cmdlets.
    - `New-AzManagementGroupDeployment` and `Test-AzManagementGroupDeployment` take the same template and parameter inputs as the resource group deployment cmdlets (`-TemplateFile`, `-TemplateUri`, `-TemplateSpecId`, `-ParametersFile`, `-ParametersUri`, `-Parameter`, `-Mode`, `-ValidationLevel`).
    - `New-AzManagementGroupDeployment` validates using `Incremental` mode before deploying with the selected `-Mode` and outputs `DeploymentExtended`.
    - `Test-AzManagementGroupDeployment` always validates using `Incremental` mode, outputs `DeploymentValidateResult`, and throws on validation errors.
    - `Get-AzManagementGroupDeployment`, `Stop-AzManagementGroupDeployment`, and `Remove-AzManagementGroupDeployment` accept `-Id`.
    - `Remove-AzManagementGroupDeployment` sends delete directly; removing a nonexistent deployment succeeds without output unless `-PassThru` is supplied.
    - `Stop-AzManagementGroupDeployment -PassThru` outputs `true` only when the cancellation runs, not under `-WhatIf` or a declined confirmation.

## Version 0.1.0

* First preview
