---
document type: cmdlet
external help file: Microsoft.Azure.Azclips.Cmdlets.Network.dll-Help.xml
HelpUri: ''
Locale: en-US
Module Name: Network
ms.date: 11/17/2026
PlatyPS schema version: 2024-05-01
title: Set-AzNetworkSecurityRule
---

# Set-AzNetworkSecurityRule

## SYNOPSIS

Update a security rule.

## SYNTAX

### __AllParameterSets

```
Set-AzNetworkSecurityRule -Name <string> -NetworkSecurityGroupName <string>
 -ResourceGroupName <string> [-Priority <int>] [-Access <string>] [-Direction <string>]
 [-Protocol <string>] [-SourceAddressPrefixes <string[]>] [-SourcePortRanges <string[]>]
 [-DestinationAddressPrefixes <string[]>] [-DestinationPortRanges <string[]>]
 [-SourceAsgs <string[]>] [-DestinationAsgs <string[]>] [-Description <string>]
 [-SubscriptionId <string>] [-Profile <string>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

## ALIASES

## DESCRIPTION

Updates an existing security rule in a network security group.
Only the parameters that you give are changed. A list parameter replaces the current list.

## EXAMPLES

### Change the destination ports of a rule

```powershell
Set-AzNetworkSecurityRule -ResourceGroupName myrg -NetworkSecurityGroupName mynsg -Name allow-https -DestinationPortRanges 443, 8443
```

### Change a rule to deny traffic

```powershell
Set-AzNetworkSecurityRule -ResourceGroupName myrg -NetworkSecurityGroupName mynsg -Name allow-https -Access Deny
```

## PARAMETERS

### -Access

Whether the rule allows or denies the traffic.

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

### -Description

A description of the rule. The maximum length is 140 characters.

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

### -DestinationAddressPrefixes

Destination CIDR ranges, IP addresses, or service tags. The new list replaces the current list.

```yaml
Type: System.String[]
DefaultValue: ''
SupportsWildcards: false
Aliases:
- DestinationAddressPrefix
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

### -DestinationAsgs

Names or resource IDs of the destination application security groups. A name resolves to an application security group in `-ResourceGroupName`.

```yaml
Type: System.String[]
DefaultValue: ''
SupportsWildcards: false
Aliases:
- DestinationApplicationSecurityGroupId
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

### -DestinationPortRanges

Destination ports or port ranges. The new list replaces the current list.

```yaml
Type: System.String[]
DefaultValue: ''
SupportsWildcards: false
Aliases:
- DestinationPortRange
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

### -Direction

Direction of the traffic that the rule applies to.

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

The name of the security rule.

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

### -Priority

New priority of the rule, from 100 to 4096. A lower number has a higher priority.

```yaml
Type: System.Int32
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

### -Protocol

Network protocol that the rule applies to. `*` matches all protocols.

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

### -SourceAddressPrefixes

Source CIDR ranges, IP addresses, or service tags. The new list replaces the current list.

```yaml
Type: System.String[]
DefaultValue: ''
SupportsWildcards: false
Aliases:
- SourceAddressPrefix
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

### -SourceAsgs

Names or resource IDs of the source application security groups. A name resolves to an application security group in `-ResourceGroupName`.

```yaml
Type: System.String[]
DefaultValue: ''
SupportsWildcards: false
Aliases:
- SourceApplicationSecurityGroupId
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

### -SourcePortRanges

Source ports or port ranges. The new list replaces the current list.

```yaml
Type: System.String[]
DefaultValue: ''
SupportsWildcards: false
Aliases:
- SourcePortRange
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

### Microsoft.Azure.Azclips.Extensions.Network.Models.SecurityRule

The updated security rule.

## NOTES


## RELATED LINKS

- [New-AzNetworkSecurityRule](./New-AzNetworkSecurityRule.md)
- [Get-AzNetworkSecurityRule](./Get-AzNetworkSecurityRule.md)
- [Remove-AzNetworkSecurityRule](./Remove-AzNetworkSecurityRule.md)
- [Get-AzNetworkSecurityGroup](./Get-AzNetworkSecurityGroup.md)

