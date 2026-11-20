---
document type: cmdlet
external help file: Microsoft.Azure.Azclips.Cmdlets.Network.dll-Help.xml
HelpUri: ''
Locale: en-US
Module Name: Network
ms.date: 11/17/2026
PlatyPS schema version: 2024-05-01
title: New-AzNetworkInterface
---

# New-AzNetworkInterface

## SYNOPSIS

Create a network interface.

## SYNTAX

### __AllParameterSets

```
New-AzNetworkInterface -Name <string> -ResourceGroupName <string> -Subnet <string>
 [-Location <string>] [-Tag <hashtable>] [-VirtualNetworkName <string>]
 [-IpConfigurationName <string>] [-PrivateIpAddress <string>] [-PrivateIpAddressVersion <string>]
 [-PublicIpAddress <string>] [-NetworkSecurityGroup <string>] [-DnsServers <string[]>]
 [-InternalDnsNameLabel <string>] [-EnableIPForwarding] [-EnableAcceleratedNetworking]
 [-EdgeZone <string>] [-SubscriptionId <string>] [-Profile <string>] [-WhatIf] [-Confirm]
 [<CommonParameters>]
```

## ALIASES

## DESCRIPTION

Creates a network interface with a single IP configuration in an existing subnet.
`-Subnet` takes a subnet resource ID, or a subnet name together with `-VirtualNetworkName`.
`-PublicIpAddress` and `-NetworkSecurityGroup` associate an existing public IP address and network security group; each takes a resource ID, or the name of a resource in `-ResourceGroupName`.

The IP configuration is named `ipconfig1` unless `-IpConfigurationName` is supplied. Its private IP address is allocated dynamically unless `-PrivateIpAddress` is supplied, in which case the allocation is `Static`.

## EXAMPLES

### Create a network interface in a subnet

Create the network interface `mynic` in the subnet `default` of the virtual network `myvnet`.

```powershell
New-AzNetworkInterface -ResourceGroupName myrg -Name mynic -VirtualNetworkName myvnet -Subnet default
```

### Create a network interface with a public IP address and a network security group

```powershell
New-AzNetworkInterface -ResourceGroupName myrg -Name mynic -VirtualNetworkName myvnet -Subnet default `
    -PublicIpAddress mypip -NetworkSecurityGroup mynsg
```

### Create a network interface with a static private IP address and accelerated networking

```powershell
$subnetId = '/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/myrg/providers/Microsoft.Network/virtualNetworks/myvnet/subnets/default'
New-AzNetworkInterface -ResourceGroupName myrg -Name mynic -Location eastus -Subnet $subnetId `
    -PrivateIpAddress 10.0.0.10 -EnableAcceleratedNetworking `
    -DnsServers 10.0.0.4, 10.0.0.5 -Tag @{ owner = 'team-a' }
```

## PARAMETERS

### -Confirm

Prompts you for confirmation before running the cmdlet.

```yaml
Type: System.Management.Automation.SwitchParameter
DefaultValue: ''
SupportsWildcards: false
Aliases:
- cf
ParameterSets:
- Name: (All)
  Position: Named
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -DnsServers

DNS server IP addresses for the network interface. When omitted, the network interface uses the DNS servers of the virtual network.

```yaml
Type: System.String[]
DefaultValue: ''
SupportsWildcards: false
Aliases:
- DnsServer
ParameterSets:
- Name: (All)
  Position: Named
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -EdgeZone

Name of the edge zone (extended location) to create the network interface in.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: (All)
  Position: Named
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -EnableAcceleratedNetworking

Enable accelerated networking on the network interface.

```yaml
Type: System.Management.Automation.SwitchParameter
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: (All)
  Position: Named
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -EnableIPForwarding

Enable IP forwarding on the network interface.

```yaml
Type: System.Management.Automation.SwitchParameter
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: (All)
  Position: Named
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -InternalDnsNameLabel

Relative DNS name for the network interface, used for internal communication between virtual machines in the same virtual network.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: (All)
  Position: Named
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -IpConfigurationName

Name of the IP configuration. Defaults to `ipconfig1`.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: (All)
  Position: Named
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -Location

Location of the network interface, e.g. `eastus`. It must match the location of the virtual network that contains the subnet. Defaults to the location of the resource group.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: (All)
  Position: Named
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -Name

Name of the network interface.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases:
- ResourceName
ParameterSets:
- Name: (All)
  Position: Named
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -NetworkSecurityGroup

Name or resource ID of an existing network security group to associate with the network interface. A name resolves to a network security group in `-ResourceGroupName`.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases:
- NetworkSecurityGroupId
ParameterSets:
- Name: (All)
  Position: Named
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -PrivateIpAddress

Static private IP address from the address range of the subnet. When omitted, a private IP address is allocated dynamically.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: (All)
  Position: Named
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -PrivateIpAddressVersion

IP version of the private IP address. Defaults to `IPv4`.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: (All)
  Position: Named
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -Profile

Name of a saved profile to use for this call instead of the currently-active one. Manage profiles with `Set-AzProfile` / `Get-AzProfile`.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: (All)
  Position: Named
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -PublicIpAddress

Name or resource ID of an existing public IP address to associate with the IP configuration. A name resolves to a public IP address in `-ResourceGroupName`.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases:
- PublicIpAddressId
ParameterSets:
- Name: (All)
  Position: Named
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -ResourceGroupName

The resource group name.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: (All)
  Position: Named
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -Subnet

Name or resource ID of the subnet for the IP configuration. A name needs `-VirtualNetworkName`, and the virtual network must be in `-ResourceGroupName`.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases:
- SubnetId
ParameterSets:
- Name: (All)
  Position: Named
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -SubscriptionId

Azure Subscription Id. Defaults to the active profile's subscription.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: (All)
  Position: Named
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -Tag

Resource tags supplied as a hashtable, e.g. @{ owner = 'team-a' }.

```yaml
Type: System.Collections.Hashtable
DefaultValue: ''
SupportsWildcards: false
Aliases:
- Tags
ParameterSets:
- Name: (All)
  Position: Named
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -VirtualNetworkName

Name of the virtual network that contains the subnet. Required when `-Subnet` is a name. Not used when `-Subnet` is a resource ID.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: (All)
  Position: Named
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -WhatIf

Runs the command in a mode that only reports what would happen without performing the actions.

```yaml
Type: System.Management.Automation.SwitchParameter
DefaultValue: ''
SupportsWildcards: false
Aliases:
- wi
ParameterSets:
- Name: (All)
  Position: Named
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### CommonParameters

This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable,
-InformationAction, -InformationVariable, -OutBuffer, -OutVariable, -PipelineVariable,
-ProgressAction, -Verbose, -WarningAction, and -WarningVariable. For more information, see
[about_CommonParameters](https://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

## OUTPUTS

### Microsoft.Azure.Azclips.Extensions.Network.Models.NetworkInterface

The network interface that was created, including its IP configuration, the allocated private IP address, and its provisioning state.

## NOTES

## RELATED LINKS

- [Get-AzNetworkInterface](./Get-AzNetworkInterface.md)
- [Remove-AzNetworkInterface](./Remove-AzNetworkInterface.md)
- [New-AzVirtualNetworkSubnet](./New-AzVirtualNetworkSubnet.md)
- [New-AzPublicIpAddress](./New-AzPublicIpAddress.md)

