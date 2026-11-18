---
document type: cmdlet
external help file: Microsoft.Azure.Azclips.Cmdlets.Network.dll-Help.xml
HelpUri: ''
Locale: en-US
Module Name: Network
ms.date: 11/17/2026
PlatyPS schema version: 2024-05-01
title: New-AzVirtualNetworkSubnet
---

# New-AzVirtualNetworkSubnet

## SYNOPSIS

Create a subnet in a virtual network.

## SYNTAX

### __AllParameterSets

```
New-AzVirtualNetworkSubnet -AddressPrefixes <string[]> -Name <string> -VirtualNetworkName <string>
 -ResourceGroupName <string> [-NetworkSecurityGroup <string>] [-RouteTable <string>]
 [-NatGateway <string>] [-ServiceEndpoints <string[]>] [-Delegations <string[]>]
 [-PrivateEndpointNetworkPolicies <string>] [-PrivateLinkServiceNetworkPolicies <string>]
 [-DefaultOutboundAccess <bool>] [-SubscriptionId <string>] [-Profile <string>] [-WhatIf] [-Confirm]
 [<CommonParameters>]
```

## ALIASES

## DESCRIPTION

Creates a subnet in an existing virtual network.
`-NetworkSecurityGroup`, `-RouteTable`, and `-NatGateway` each take a resource ID, or the name of a resource in `-ResourceGroupName`.

## EXAMPLES

### Create a subnet

Create the subnet `web` with the address prefix 10.0.1.0/24.

```powershell
New-AzVirtualNetworkSubnet -ResourceGroupName myrg -VirtualNetworkName myvnet -Name web -AddressPrefixes 10.0.1.0/24
```

### Create a subnet with a network security group and a service endpoint

```powershell
New-AzVirtualNetworkSubnet -ResourceGroupName myrg -VirtualNetworkName myvnet -Name data `
    -AddressPrefixes 10.0.2.0/24 -NetworkSecurityGroup mynsg -ServiceEndpoints Microsoft.Storage
```

## PARAMETERS

### -AddressPrefixes

Address prefixes for the subnet, e.g. 10.0.1.0/24. They must be inside the address space of the virtual network.

```yaml
Type: System.String[]
DefaultValue: ''
SupportsWildcards: false
Aliases:
- AddressPrefix
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

### -DefaultOutboundAccess

Whether VMs in the subnet get default outbound access to the internet. Use `$false` to make the subnet private.

```yaml
Type: System.Nullable`1[System.Boolean]
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

### -Delegations

Services to delegate the subnet to, e.g. `Microsoft.Web/serverFarms`.

```yaml
Type: System.String[]
DefaultValue: ''
SupportsWildcards: false
Aliases:
- Delegation
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

The name of the subnet.

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

### -NatGateway

Name or resource ID of the NAT gateway to attach to the subnet. A name resolves to a NAT gateway in `-ResourceGroupName`.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases:
- NatGatewayId
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

### -NetworkSecurityGroup

Name or resource ID of the network security group to attach to the subnet. A name resolves to a network security group in `-ResourceGroupName`.

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

### -PrivateEndpointNetworkPolicies

Network policies for private endpoints in the subnet.

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

### -PrivateLinkServiceNetworkPolicies

Network policies for private link services in the subnet.

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

### -RouteTable

Name or resource ID of the route table to attach to the subnet. A name resolves to a route table in `-ResourceGroupName`.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases:
- RouteTableId
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

### -ServiceEndpoints

Service endpoints to turn on for the subnet, e.g. `Microsoft.Storage`.

```yaml
Type: System.String[]
DefaultValue: ''
SupportsWildcards: false
Aliases:
- ServiceEndpoint
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

### -VirtualNetworkName

The name of the virtual network that contains the subnet.

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

### Microsoft.Azure.Azclips.Extensions.Network.Models.Subnet

The created subnet.

## NOTES


## RELATED LINKS

- [Get-AzVirtualNetworkSubnet](./Get-AzVirtualNetworkSubnet.md)
- [Set-AzVirtualNetworkSubnet](./Set-AzVirtualNetworkSubnet.md)
- [Remove-AzVirtualNetworkSubnet](./Remove-AzVirtualNetworkSubnet.md)
- [Get-AzVirtualNetwork](./Get-AzVirtualNetwork.md)

