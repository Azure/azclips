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

* Added management-group deployment create, get, stop, remove, and test cmdlets.
    - `New-AzManagementGroupDeployment` and `Get-AzManagementGroupDeployment` output `PSDeployment`, matching Azure PowerShell.
    - `New-AzManagementGroupDeployment` supports `-AsJob`, `-TemplateSpecId`, template parameters as dynamic parameters, and verbose deployment progress.
    - `Remove-AzManagementGroupDeployment` supports `-AsJob`.
    - `Test-AzManagementGroupDeployment` outputs validation errors and writes nothing when the template is valid.
    - `New-AzManagementGroupDeployment` and `Test-AzManagementGroupDeployment` support `-SkipTemplateParameterPrompt`; `Test-AzManagementGroupDeployment` supports `-SuppressDiagnostics`.
    - Management group deployment cmdlets accept `-Pre` for compatibility.

## Version 0.1.0

* First preview