---
document type: cmdlet
external help file: Microsoft.Azure.Azclips.Cmdlets.Network.dll-Help.xml
HelpUri: ''
Locale: en-US
Module Name: Network
ms.date: 11/17/2026
PlatyPS schema version: 2024-05-01
title: Get-AzNetworkInterface
---

# Get-AzNetworkInterface

## SYNOPSIS

Get a network interface, or list the network interfaces in a resource group or subscription.

## SYNTAX

### List (Default)

```
Get-AzNetworkInterface [-ResourceGroupName <string>] [-SubscriptionId <string>] [-Profile <string>]
 [<CommonParameters>]
```

### Get

```
Get-AzNetworkInterface -ResourceGroupName <string> -Name <string> [-ExpandResource <string>]
 [-SubscriptionId <string>] [-Profile <string>] [<CommonParameters>]
```

## ALIASES

## DESCRIPTION

Gets a network interface by name, or lists the network interfaces in a resource group or, when `-ResourceGroupName` is omitted, in the whole subscription.

## EXAMPLES

### Get a network interface

Show the network interface `mynic` in resource group `myrg`.

```powershell
Get-AzNetworkInterface -ResourceGroupName myrg -Name mynic
```

### Expand the associated public IP address

```powershell
Get-AzNetworkInterface -ResourceGroupName myrg -Name mynic -ExpandResource ipConfigurations/publicIPAddress
```

### List the network interfaces in a resource group

```powershell
Get-AzNetworkInterface -ResourceGroupName myrg
```

### List every network interface in the subscription

```powershell
Get-AzNetworkInterface
```

## PARAMETERS

### -ExpandResource

Expands referenced resources in the response, e.g. `ipConfigurations/publicIPAddress`.

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

Name of the network interface.

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
- Name: Get
  Position: Named
  IsRequired: true
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

### CommonParameters

This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable,
-InformationAction, -InformationVariable, -OutBuffer, -OutVariable, -PipelineVariable,
-ProgressAction, -Verbose, -WarningAction, and -WarningVariable. For more information, see
[about_CommonParameters](https://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

## OUTPUTS

### Microsoft.Azure.Azclips.Extensions.Network.Models.NetworkInterface

The requested network interface, or every network interface in the resource group or subscription when `-Name` is omitted.

## NOTES

## RELATED LINKS

- [New-AzNetworkInterface](./New-AzNetworkInterface.md)
- [Remove-AzNetworkInterface](./Remove-AzNetworkInterface.md)
- [New-AzVirtualNetworkSubnet](./New-AzVirtualNetworkSubnet.md)
- [New-AzPublicIpAddress](./New-AzPublicIpAddress.md)

