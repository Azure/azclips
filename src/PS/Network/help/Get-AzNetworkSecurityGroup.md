---
document type: cmdlet
external help file: Microsoft.Azure.Azclips.Cmdlets.Network.dll-Help.xml
HelpUri: ''
Locale: en-US
Module Name: Network
ms.date: 11/17/2026
PlatyPS schema version: 2024-05-01
title: Get-AzNetworkSecurityGroup
---

# Get-AzNetworkSecurityGroup

## SYNOPSIS

Get a network security group, or list the network security groups in a resource group or subscription.

## SYNTAX

### List (Default)

```
Get-AzNetworkSecurityGroup [-ResourceGroupName <string>] [-SubscriptionId <string>]
 [-Profile <string>] [<CommonParameters>]
```

### Get

```
Get-AzNetworkSecurityGroup -ResourceGroupName <string> -Name <string> [-ExpandResource <string>]
 [-SubscriptionId <string>] [-Profile <string>] [<CommonParameters>]
```

## ALIASES

## DESCRIPTION

Gets a network security group by name, or lists the network security groups in a resource group or, when `-ResourceGroupName` is omitted, in the whole subscription.

## EXAMPLES

### Get a network security group

Show the network security group `mynsg` in resource group `myrg`.

```powershell
Get-AzNetworkSecurityGroup -ResourceGroupName myrg -Name mynsg
```

### Expand the associated network interfaces

```powershell
Get-AzNetworkSecurityGroup -ResourceGroupName myrg -Name mynsg -ExpandResource networkInterfaces
```

### List the network security groups in a resource group

```powershell
Get-AzNetworkSecurityGroup -ResourceGroupName myrg
```

### List every network security group in the subscription

```powershell
Get-AzNetworkSecurityGroup
```

## PARAMETERS

### -ExpandResource

Expands referenced resources in the response, e.g. `networkInterfaces` or `subnets`.

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

Name of the network security group.

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

### Microsoft.Azure.Azclips.Extensions.Network.Models.NetworkSecurityGroup

The requested network security group, or every network security group in the resource group or subscription when `-Name` is omitted.

## NOTES

## RELATED LINKS

- [New-AzNetworkSecurityGroup](./New-AzNetworkSecurityGroup.md)
- [Remove-AzNetworkSecurityGroup](./Remove-AzNetworkSecurityGroup.md)
- [New-AzNetworkSecurityRule](./New-AzNetworkSecurityRule.md)

