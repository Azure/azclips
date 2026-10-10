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

* Add tenant-scope ARM deployment commands, aligned with legacy Az.Resources.
    - Supports New/Get/Stop/Remove/Test-AzTenantDeployment with legacy parameter sets, dynamic template parameters, `-AsJob`, `-WhatIf`/`-Confirm` What-If preview, and `PSDeployment`/`PSResourceManagerError` output.

## Version 0.1.0

* First preview