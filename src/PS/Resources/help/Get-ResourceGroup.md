---
document type: cmdlet
external help file: Microsoft.Azure.Cirrus.Cmdlets.Resources.dll-Help.xml
HelpUri: ""
Locale: en-AU
Module Name: Resources
ms.date: 06/03/2026
PlatyPS schema version: 2024-05-01
title: Get-ResourceGroup
---

# Get-ResourceGroup

## SYNOPSIS

Show details of a specific resource group.

## SYNTAX

### List (Default)

```
Get-ResourceGroup -SubscriptionId <string> [<CommonParameters>]
```

### Get

```
Get-ResourceGroup -SubscriptionId <string> -Name <string> [<CommonParameters>]
```

## ALIASES

This cmdlet has the following aliases,
{{Insert list of aliases}}

## DESCRIPTION

Display information about a resource group including its name, location, provisioning state, and resource count.
Specify the resource group name and optionally the subscription ID (defaults to current subscription if not provided).

## EXAMPLES

### Show a resource group

TODO - describe the example.

```powershell
Get-ResourceGroup -SubscriptionId <subscriptionid> -Name <name>
```

### List all resource groups in a subscription

TODO - describe the example.

```powershell
Get-ResourceGroup -SubscriptionId <subscriptionid>
```

## PARAMETERS

### -Name

The name of the resource group.

```yaml
Type: System.String
DefaultValue: ""
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
HelpMessage: ""
```

### -SubscriptionId

Azure Subscription Id.

```yaml
Type: System.String
DefaultValue: ""
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
HelpMessage: ""
```

### CommonParameters

This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable,
-InformationAction, -InformationVariable, -OutBuffer, -OutVariable, -PipelineVariable,
-ProgressAction, -Verbose, -WarningAction, and -WarningVariable. For more information, see
[about_CommonParameters](https://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

## OUTPUTS

## NOTES

Generated help for resources.

## RELATED LINKS

- [Online Version]()
