---
document type: cmdlet
external help file: Microsoft.Azure.Azclips.Cmdlets.Network.dll-Help.xml
HelpUri: ''
Locale: en-US
Module Name: Network
ms.date: 11/17/2026
PlatyPS schema version: 2024-05-01
title: Get-AzVirtualNetworkSubnet
---

# Get-AzVirtualNetworkSubnet

## SYNOPSIS

Get a subnet, or list the subnets of a virtual network.

## SYNTAX

### List (Default)

```
Get-AzVirtualNetworkSubnet -VirtualNetworkName <string> -ResourceGroupName <string>
 [-SubscriptionId <string>] [-Profile <string>] [<CommonParameters>]
```

### Get

```
Get-AzVirtualNetworkSubnet -Name <string> -VirtualNetworkName <string> -ResourceGroupName <string>
 [-ExpandResource <string>] [-SubscriptionId <string>] [-Profile <string>] [<CommonParameters>]
```

## ALIASES

## DESCRIPTION

Gets a subnet by name, or lists all subnets of a virtual network when `-Name` is omitted.

## EXAMPLES

### Get a subnet

Show the subnet `web` in the virtual network `myvnet`.

```powershell
Get-AzVirtualNetworkSubnet -ResourceGroupName myrg -VirtualNetworkName myvnet -Name web
```

### List the subnets of a virtual network

```powershell
Get-AzVirtualNetworkSubnet -ResourceGroupName myrg -VirtualNetworkName myvnet
```

## PARAMETERS

### -ExpandResource

Expands referenced resources in the response, e.g. `networkSecurityGroup`.

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

The name of the subnet. When omitted, the cmdlet lists the subnets of the virtual network.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases:
- ResourceName
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

### CommonParameters

This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable,
-InformationAction, -InformationVariable, -OutBuffer, -OutVariable, -PipelineVariable,
-ProgressAction, -Verbose, -WarningAction, and -WarningVariable. For more information, see
[about_CommonParameters](https://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

## OUTPUTS

### Microsoft.Azure.Azclips.Extensions.Network.Models.Subnet

The matching subnet, or each subnet of the virtual network when listing.

## NOTES


## RELATED LINKS

- [New-AzVirtualNetworkSubnet](./New-AzVirtualNetworkSubnet.md)
- [Set-AzVirtualNetworkSubnet](./Set-AzVirtualNetworkSubnet.md)
- [Remove-AzVirtualNetworkSubnet](./Remove-AzVirtualNetworkSubnet.md)
- [Get-AzVirtualNetwork](./Get-AzVirtualNetwork.md)

