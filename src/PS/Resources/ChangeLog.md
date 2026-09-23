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
* Added `Get-AzRoleAssignment` to list Azure RBAC role assignments at a subscription, resource group, or scope
    - Supports `-Scope`, `-ResourceGroupName`, `-SubscriptionId`, and `-ObjectId` (principal object ID); name/UPN/service-principal and role-name filtering are not yet supported

## Version 0.1.0

* First preview