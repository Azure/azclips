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
* Added tenant deployment cmdlets: `New-AzTenantDeployment`, `Test-AzTenantDeployment`, `Get-AzTenantDeploymentWhatIfResult`, `Get-AzTenantDeployment`, `Stop-AzTenantDeployment`, `Remove-AzTenantDeployment`, and `Save-AzTenantDeploymentTemplate`.
    - `New-`, `Test-`, and `Get-AzTenantDeploymentWhatIfResult` take a template from `-TemplateFile`, `-TemplateUri`, `-TemplateSpecId`, or `-TemplateObject`, and parameters from `-ParametersFile`, `-ParametersUri`, `-Parameter`, or `-TemplateParameterObject`. `-TemplateParameterFile`, `-TemplateParameterUri`, and `-DeploymentName` are accepted as aliases. Tenant deployments always use `Incremental` mode, like legacy.
    - Each parameter of a local template, or of the template a `.bicepparam` parameters file names in its `using` declaration, becomes a cmdlet parameter; missing required ones are prompted for unless `-SkipTemplateParameterPrompt` is supplied.
    - `New-AzTenantDeployment` validates before deploying, supports `-Tag`, `-DeploymentDebugLogLevel`, and `-AsJob`, and outputs `PSDeployment`. `-Verbose` shows each resource's provisioning status while the deployment runs.
    - `New-AzTenantDeployment -WhatIf` and `-Confirm` show the what-if change report, with `-WhatIfResultFormat`, `-WhatIfExcludeChangeType`, and `-ProceedIfNoChange`.
    - `Test-AzTenantDeployment` writes the validation error as `PSResourceManagerError`, or nothing when valid plus a diagnostics warning unless `-SuppressDiagnostics` is supplied.
    - `Get-AzTenantDeploymentWhatIfResult` outputs `WhatIfOperationResult`, with `-ResultFormat` and `-ExcludeChangeType`.
    - `Get-AzTenantDeployment` outputs `PSDeployment`. `Get-`, `Stop-`, and `Remove-AzTenantDeployment` accept `-Id`.
    - `Remove-AzTenantDeployment` supports `-AsJob`; removing a nonexistent deployment succeeds without output. `Stop-` and `Remove-AzTenantDeployment -PassThru` output `true` only when the action runs.
    - `Save-AzTenantDeploymentTemplate` saves a deployment's template to a `.json` file, with `-Path`, `-Force`, and piped deployment objects. 

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
