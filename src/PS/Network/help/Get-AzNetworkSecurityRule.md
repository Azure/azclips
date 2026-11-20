---
document type: cmdlet
external help file: Microsoft.Azure.Azclips.Cmdlets.Network.dll-Help.xml
HelpUri: ''
Locale: en-US
Module Name: Network
ms.date: 11/17/2026
PlatyPS schema version: 2024-05-01
title: Get-AzNetworkSecurityRule
---

# Get-AzNetworkSecurityRule

## SYNOPSIS

Get a security rule, or list the security rules of a network security group.

## SYNTAX

### List (Default)

```
Get-AzNetworkSecurityRule -NetworkSecurityGroupName <string> -ResourceGroupName <string>
 [-SubscriptionId <string>] [-Profile <string>] [<CommonParameters>]
```

### Get

```
Get-AzNetworkSecurityRule -Name <string> -NetworkSecurityGroupName <string>
 -ResourceGroupName <string> [-SubscriptionId <string>] [-Profile <string>] [<CommonParameters>]
```

## ALIASES

## DESCRIPTION

Gets a security rule by name, or lists the security rules of a network security group when `-Name` is omitted.
The list does not include the default security rules.

## EXAMPLES

### Get a security rule

```powershell
Get-AzNetworkSecurityRule -ResourceGroupName myrg -NetworkSecurityGroupName mynsg -Name allow-https
```

### List the security rules of a network security group

```powershell
Get-AzNetworkSecurityRule -ResourceGroupName myrg -NetworkSecurityGroupName mynsg
```

## PARAMETERS

### -Name

The name of the security rule. When omitted, the cmdlet lists the security rules of the network security group.

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

### -NetworkSecurityGroupName

The name of the network security group that contains the rule.

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

### CommonParameters

This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable,
-InformationAction, -InformationVariable, -OutBuffer, -OutVariable, -PipelineVariable,
-ProgressAction, -Verbose, -WarningAction, and -WarningVariable. For more information, see
[about_CommonParameters](https://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

## OUTPUTS

### Microsoft.Azure.Azclips.Extensions.Network.Models.SecurityRule

The matching security rule, or each security rule of the network security group when listing.

## NOTES


## RELATED LINKS

- [New-AzNetworkSecurityRule](./New-AzNetworkSecurityRule.md)
- [Set-AzNetworkSecurityRule](./Set-AzNetworkSecurityRule.md)
- [Remove-AzNetworkSecurityRule](./Remove-AzNetworkSecurityRule.md)
- [Get-AzNetworkSecurityGroup](./Get-AzNetworkSecurityGroup.md)

