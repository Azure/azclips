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

## Version 0.1.3
* Removed `New-AzVirtualNetworkSubnetConfig`
    - Pass hashtables to `New-AzVirtualNetwork -Subnet` instead.
* Fixed `Get-AzPublicIpAddress` output exposing a blank `Properties` value instead of the assigned IP address.
    - Resource properties are available at the top level.
    - The attached IP configuration is flattened into `IpConfigurationId`, `PrivateIPAddress` and `SubnetId` (the last two with `-ExpandResource`); the nested `IpConfiguration` object is no longer exposed.
    - `ServicePublicIPAddress` and `LinkedPublicIPAddress` are no longer exposed.
* Updated `New-AzVirtualNetwork`, `Get-AzVirtualNetwork` and `Remove-AzVirtualNetwork`
    - `-Name` accepts the `ResourceName` alias.
    - `-Subnet` hashtables accept array values for multi-valued keys such as `AddressPrefixes`.
    - Subnets no longer send private endpoint / private link service network policies unless requested.
* Virtual network, subnet, network security group, network interface and public IP address outputs now use the TypeSpec-generated models
    - Resource properties are exposed directly on the output object (for example `$vnet.Subnets`, `$subnet.AddressPrefixes`) instead of under `.Properties`.
    - The public IP address output type is now `PublicIPAddress`.

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
