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

* Added tenant deployment create, get, stop, remove, and test cmdlets.
    - `New-AzTenantDeployment` and `Test-AzTenantDeployment` take the same template and parameter inputs as the resource group deployment cmdlets (`-TemplateFile`, `-TemplateUri`, `-TemplateSpecId`, `-ParametersFile`, `-ParametersUri`, `-Parameter`, `-ValidationLevel`). Tenant deployments always use `Incremental` mode, like legacy.
    - `New-AzTenantDeployment` validates before deploying and outputs `DeploymentExtended`.
    - `Test-AzTenantDeployment` outputs `DeploymentValidateResult` and throws on validation errors.
    - `Get-AzTenantDeployment`, `Stop-AzTenantDeployment`, and `Remove-AzTenantDeployment` accept `-Id`.

## Version 0.1.0

* First preview