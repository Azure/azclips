---
document type: cmdlet
external help file: Microsoft.Azure.Azclips.Cmdlets.Network.dll-Help.xml
HelpUri: ''
Locale: en-US
Module Name: Network
ms.date: 10/26/2026
PlatyPS schema version: 2024-05-01
title: Get-AzVirtualNetworkSubnetConfig
---

# Get-AzVirtualNetworkSubnetConfig

## SYNOPSIS

Get a subnet, or list the subnets in a virtual network.

## SYNTAX

### List (Default)

```
Get-AzVirtualNetworkSubnetConfig -ResourceGroupName <string> -VirtualNetworkName <string>
 [-SubscriptionId <string>] [-Profile <string>] [<CommonParameters>]
```

### Get

```
Get-AzVirtualNetworkSubnetConfig -ResourceGroupName <string> -VirtualNetworkName <string> -Name <string>
 [-SubscriptionId <string>] [-ExpandResource <string>] [-Profile <string>] [<CommonParameters>]
```

## ALIASES

## DESCRIPTION

Gets a subnet by name from Azure, or lists every subnet in the specified virtual network.
Unlike legacy Az, this cmdlet reads from Azure by `-ResourceGroupName` and `-VirtualNetworkName`; it does not accept a `-VirtualNetwork` object or `-ResourceId`.

## EXAMPLES

### Get a subnet

Show the subnet `web` in virtual network `myvnet`.

```powershell
Get-AzVirtualNetworkSubnetConfig -ResourceGroupName myrg -VirtualNetworkName myvnet -Name web
```

### Expand the subnet's network security group

```powershell
Get-AzVirtualNetworkSubnetConfig -ResourceGroupName myrg -VirtualNetworkName myvnet -Name web -ExpandResource networkSecurityGroup
```

### List the subnets in a virtual network

```powershell
Get-AzVirtualNetworkSubnetConfig -ResourceGroupName myrg -VirtualNetworkName myvnet
```

## PARAMETERS

### -ExpandResource

Expands referenced resources in the response, e.g. `networkSecurityGroup` or `routeTable`.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: Get
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

Name of the subnet.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: Get
  Position: Named
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -Profile

Azure profile to target, e.g. `latest`.

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

Name of the resource group that contains the virtual network.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: Get
  Position: Named
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
- Name: List
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

ID of the subscription that contains the virtual network. Defaults to the subscription of the current context.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: Get
  Position: Named
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
- Name: List
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

Name of the virtual network that contains the subnet.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: Get
  Position: Named
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
- Name: List
  Position: Named
  IsRequired: true
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

The requested subnet, or every subnet in the virtual network when `-Name` is omitted.

## NOTES

Generated help for network.

## RELATED LINKS


- [Online Version]()
