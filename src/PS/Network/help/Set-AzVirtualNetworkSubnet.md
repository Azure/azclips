---
document type: cmdlet
external help file: Microsoft.Azure.Azclips.Cmdlets.Network.dll-Help.xml
HelpUri: ''
Locale: en-US
Module Name: Network
ms.date: 11/17/2026
PlatyPS schema version: 2024-05-01
title: Set-AzVirtualNetworkSubnet
---

# Set-AzVirtualNetworkSubnet

## SYNOPSIS

Update a subnet.

## SYNTAX

### __AllParameterSets

```
Set-AzVirtualNetworkSubnet -Name <string> -VirtualNetworkName <string> -ResourceGroupName <string>
 [-AddressPrefixes <string[]>] [-NetworkSecurityGroup <string>] [-RouteTable <string>]
 [-NatGateway <string>] [-ServiceEndpoints <string[]>] [-Delegations <string[]>]
 [-PrivateEndpointNetworkPolicies <string>] [-PrivateLinkServiceNetworkPolicies <string>]
 [-DefaultOutboundAccess <bool>] [-SubscriptionId <string>] [-Profile <string>] [-WhatIf] [-Confirm]
 [<CommonParameters>]
```

## ALIASES

## DESCRIPTION

Updates the properties of an existing subnet in a virtual network.
Only the parameters that you give are changed.
To detach a network security group, route table, or NAT gateway, give an empty string ('') to its parameter.

## EXAMPLES

### Attach a network security group to a subnet

```powershell
Set-AzVirtualNetworkSubnet -ResourceGroupName myrg -VirtualNetworkName myvnet -Name web -NetworkSecurityGroup mynsg
```

### Detach the route table and add a service endpoint

```powershell
Set-AzVirtualNetworkSubnet -ResourceGroupName myrg -VirtualNetworkName myvnet -Name web `
    -RouteTable '' -ServiceEndpoints Microsoft.Storage
```

## PARAMETERS

### -AddressPrefixes

New address prefixes for the subnet. They replace the current prefixes.

```yaml
Type: System.String[]
DefaultValue: ''
SupportsWildcards: false
Aliases:
- AddressPrefix
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

Services to delegate the subnet to, e.g. `Microsoft.Web/serverFarms`. The new list replaces the current list.

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

Name or resource ID of the NAT gateway to attach to the subnet. A name resolves to a NAT gateway in `-ResourceGroupName`. Use an empty string ('') to detach it.

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

Name or resource ID of the network security group to attach to the subnet. A name resolves to a network security group in `-ResourceGroupName`. Use an empty string ('') to detach it.

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

Name or resource ID of the route table to attach to the subnet. A name resolves to a route table in `-ResourceGroupName`. Use an empty string ('') to detach it.

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

Service endpoints to turn on for the subnet, e.g. `Microsoft.Storage`. The new list replaces the current list.

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

The updated subnet.

## NOTES


## RELATED LINKS

- [New-AzVirtualNetworkSubnet](./New-AzVirtualNetworkSubnet.md)
- [Get-AzVirtualNetworkSubnet](./Get-AzVirtualNetworkSubnet.md)
- [Remove-AzVirtualNetworkSubnet](./Remove-AzVirtualNetworkSubnet.md)
- [Get-AzVirtualNetwork](./Get-AzVirtualNetwork.md)

