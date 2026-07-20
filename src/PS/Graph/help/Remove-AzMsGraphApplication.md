---
document type: cmdlet
external help file: Microsoft.Azure.Azclips.Cmdlets.Graph.dll-Help.xml
HelpUri: ''
Locale: en-US
Module Name: Graph
ms.date: 07/20/2026
PlatyPS schema version: 2024-05-01
title: Remove-AzMsGraphApplication
---

# Remove-AzMsGraphApplication

## SYNOPSIS

Deletes a Microsoft Graph application registration.

## SYNTAX

```
Remove-AzMsGraphApplication -Id <string> [<CommonParameters>]
```

## ALIASES

## DESCRIPTION

Deletes an application registration from Microsoft Graph via `DELETE /applications/{id}` using the active Azure profile (see `Connect-AzAccount`). The identifier is the application's object ID. Graph performs a soft delete; the application is recoverable for 30 days.

## EXAMPLES

### Example 1: Delete an application by object ID

```powershell
Remove-AzMsGraphApplication -Id 00000000-0000-0000-0000-000000000000
```

## PARAMETERS

### -Id

The object ID of the application registration to delete.

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

## INPUTS

### None

## OUTPUTS

### System.Void

## NOTES

## RELATED LINKS
