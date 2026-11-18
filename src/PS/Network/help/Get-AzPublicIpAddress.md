---
document type: cmdlet
external help file: Microsoft.Azure.Azclips.Cmdlets.Network.dll-Help.xml
HelpUri: ''
Locale: en-US
Module Name: Network
ms.date: 11/17/2026
PlatyPS schema version: 2024-05-01
title: Get-AzPublicIpAddress
---

# Get-AzPublicIpAddress

## SYNOPSIS

Get a public IP address, or list the public IP addresses in a resource group or subscription.

## SYNTAX

### List (Default)

```
Get-AzPublicIpAddress [-ResourceGroupName <string>] [-SubscriptionId <string>] [-Profile <string>]
 [<CommonParameters>]
```

### Get

```
Get-AzPublicIpAddress -ResourceGroupName <string> -Name <string> [-ExpandResource <string>]
 [-SubscriptionId <string>] [-Profile <string>] [<CommonParameters>]
```

## ALIASES

## DESCRIPTION

Gets a public IP address by name, or lists the public IP addresses in a resource group or, when `-ResourceGroupName` is omitted, in the whole subscription.

## EXAMPLES

### Get a public IP address

Show the public IP address `mypip` in resource group `myrg`.

```powershell
Get-AzPublicIpAddress -ResourceGroupName myrg -Name mypip
```

### Expand the attached IP configuration

```powershell
Get-AzPublicIpAddress -ResourceGroupName myrg -Name mypip -ExpandResource ipConfiguration
```

### List the public IP addresses in a resource group

```powershell
Get-AzPublicIpAddress -ResourceGroupName myrg
```

### List every public IP address in the subscription

```powershell
Get-AzPublicIpAddress
```

## PARAMETERS

### -ExpandResource

Expands referenced resources in the response, e.g. `ipConfiguration`.

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

Name of the public IP address.

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

### Microsoft.Azure.Azclips.Extensions.Network.Models.PublicIPAddress

The requested public IP address, or every public IP address in the resource group or subscription when `-Name` is omitted.

## NOTES

## RELATED LINKS

- [New-AzPublicIpAddress](./New-AzPublicIpAddress.md)
- [Remove-AzPublicIpAddress](./Remove-AzPublicIpAddress.md)
- [New-AzNetworkInterface](./New-AzNetworkInterface.md)

