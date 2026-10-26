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
* Added `New-AzVirtualNetworkSubnetConfig`.
* Added `New-AzNetworkInterface`, `Get-AzNetworkInterface` and `Remove-AzNetworkInterface`
    - `New-AzNetworkInterface` creates a network interface with a single IP configuration bound to `-SubnetId`, optionally associating a public IP address (`-PublicIpAddressId`) and a network security group (`-NetworkSecurityGroupId`).
    - `Get-AzNetworkInterface` serves both the single-interface `Get` set and the `List` set; omitting `-ResourceGroupName` lists every network interface in the subscription.
* Added `New-AzNetworkSecurityGroup`, `Get-AzNetworkSecurityGroup` and `Remove-AzNetworkSecurityGroup`
    - `Get-AzNetworkSecurityGroup` serves both the single-group `Get` set and the `List` set; omitting `-ResourceGroupName` lists every network security group in the subscription.

## Version 0.1.0
* Added `New-AzPublicIpAddress`, `Get-AzPublicIpAddress` and `Remove-AzPublicIpAddress`
    - `Get-AzPublicIpAddress` serves both the single-address `Get` set and the `List` set; omitting `-ResourceGroupName` lists every public IP address in the subscription.

## Version 0.1.0

* First preview
