---
document type: cmdlet
external help file: Microsoft.Azure.Azclips.Cmdlets.Network.dll-Help.xml
HelpUri: ''
Locale: en-US
Module Name: Network
ms.date: 11/24/2026
PlatyPS schema version: 2024-05-01
title: Get-AzVirtualNetwork
---

# Get-AzVirtualNetwork

## SYNOPSIS

Gets a virtual network or lists virtual networks.

## SYNTAX

### List (Default)

```
Get-AzVirtualNetwork [-ResourceGroupName <string>] [-SubscriptionId <string>] [-Profile <string>]
 [<CommonParameters>]
```

### Get

```
Get-AzVirtualNetwork -ResourceGroupName <string> -Name <string> [-ExpandResource <string>]
 [-SubscriptionId <string>] [-Profile <string>] [<CommonParameters>]
```

## ALIASES

This cmdlet has the following aliases,
  {{Insert list of aliases}}

## DESCRIPTION

The Get-AzVirtualNetwork cmdlet gets one virtual network when you specify -ResourceGroupName and -Name. Without -Name, it lists the virtual networks in a resource group, or in the whole subscription when you do not specify -ResourceGroupName.

## EXAMPLES

### Example 1: List all virtual networks in the subscription

This command lists every virtual network in the current subscription.

```powershell
Get-AzVirtualNetwork
```

### Example 2: List virtual networks in a resource group

This command lists the virtual networks in the resource group myrg.

```powershell
Get-AzVirtualNetwork -ResourceGroupName myrg
```

### Example 3: Get one virtual network

This command gets the virtual network named myvnet in the resource group myrg.

```powershell
Get-AzVirtualNetwork -ResourceGroupName myrg -Name myvnet -SubscriptionId 00000000-0000-0000-0000-000000000000
```

## PARAMETERS

### -ExpandResource

Expands referenced resources.

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

The resource name.

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

### Microsoft.Azure.Azclips.Extensions.Network.Models.VirtualNetwork

The virtual network objects that match the query.

## NOTES

## RELATED LINKS

- [New-AzVirtualNetwork](./New-AzVirtualNetwork.md)
- [Remove-AzVirtualNetwork](./Remove-AzVirtualNetwork.md)

