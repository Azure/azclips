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
    - `Stop-AzDeployment` and `Remove-AzDeployment` accept `-Id` or a deployment piped from `Get-AzDeployment` (`-InputObject`), and `-PassThru` outputs `true` when the cancel or delete runs
    - `New-AzDeployment` reports the deployment's ARM error, up to 3 failed operation errors, and the correlation id when the deployment fails
    - `New-AzDeployment` validates the deployment before submitting it and throws `Deployment validation failed.` without creating a deployment when validation fails
* When `-Name` is omitted, `New-AzDeployment`, `Test-AzDeployment`, `Test-AzResourceGroupDeployment`, and `Test-AzManagementGroupDeployment` name the deployment after the template file or URI instead of a GUID
    - A GUID is still used when no template file or URI is given, or the derived name is not a valid deployment name
* Inline `-Parameter` `Name=Value` values for deployment cmdlets are typed by the template's declared parameter type when the template is local
    - A value that does not fit an `int`, `bool`, `object` or `array` parameter throws an error naming the parameter
* `New-AzDeployment` and `Test-AzDeployment` throw an error asking to upgrade Bicep CLI when a `.bicepparam` file is used with Bicep CLI older than 0.16.1, or with inline `-Parameter` values and Bicep CLI older than 0.22.6
    - No request is sent; the installed version is read once per invocation

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
