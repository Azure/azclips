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
* Added `Get-AzVirtualNetworkSubnetConfig` and `Remove-AzVirtualNetworkSubnetConfig`
    - Unlike legacy Az, both act on Azure directly by `-ResourceGroupName` and `-VirtualNetworkName` instead of on a `-VirtualNetwork` object; `Remove-AzVirtualNetworkSubnetConfig` deletes the subnet immediately and needs no `Set-AzVirtualNetwork`.
    - `Get-AzVirtualNetworkSubnetConfig` serves the single-subnet `Get` set (with `-ExpandResource`) and the `List` set.
* `New-AzVirtualNetworkSubnetConfig` no longer contacts Azure (profile or data-boundary lookup) and normalizes dotted `-DelegatedService` values such as `Microsoft.Web.serverFarms`.

## Version 0.1.2
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
