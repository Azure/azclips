---
document type: cmdlet
external help file: Microsoft.Azure.Cirrus.Cmdlets.Compute.dll-Help.xml
HelpUri: ''
Locale: en-AU
Module Name: Compute
ms.date: 06/04/2026
PlatyPS schema version: 2024-05-01
title: Get-VM
---

# Get-VM

## SYNOPSIS

Show details for a virtual machine.

## SYNTAX

### List (Default)

```
Get-VM -SubscriptionId <string> -ResourceGroupName <string> [<CommonParameters>]
```

### Get

```
Get-VM -SubscriptionId <string> -ResourceGroupName <string> -Name <string> [<CommonParameters>]
```

## ALIASES

This cmdlet has the following aliases,
  {{Insert list of aliases}}

## DESCRIPTION

Retrieves the properties of a single virtual machine identified by name within the specified resource group and subscription.

## EXAMPLES

### Get-VM

TODO - describe the example.

```powershell
Get-VM -SubscriptionId <subscriptionid> -ResourceGroupName <resourcegroupname> -Name <name>
```

## PARAMETERS

### -Name

The name of the virtual machine.

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
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -SubscriptionId

Azure Subscription Id.

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

## NOTES

Generated help for compute.
Daniel


## RELATED LINKS


- [Online Version]()
