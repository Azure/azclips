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

* Added `New-AzSqlDatabase` to create an Azure SQL database
    - The database location is inherited from its server, matching `az sql db create`
* Added `New-AzSqlServerFirewallRule` to create a firewall rule on an Azure SQL server
    - `-AllowAllAzureIPs` creates the well-known `AllowAllWindowsAzureIps` rule (0.0.0.0-0.0.0.0)

## Version 0.1.0

* First preview
