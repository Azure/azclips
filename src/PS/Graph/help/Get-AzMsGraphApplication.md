---
document type: cmdlet
external help file: Microsoft.Azure.Azclips.Cmdlets.Graph.dll-Help.xml
HelpUri: ''
Locale: en-US
Module Name: Graph
ms.date: 07/20/2026
PlatyPS schema version: 2024-05-01
title: Get-AzMsGraphApplication
---

# Get-AzMsGraphApplication

## SYNOPSIS

Gets a Microsoft Graph application registration by object ID.

## SYNTAX

```
Get-AzMsGraphApplication -Id <string> [<CommonParameters>]
```

## ALIASES

## DESCRIPTION

Retrieves a single application registration from Microsoft Graph via `GET /applications/{id}` using the active Azure profile (see `Connect-AzAccount`). The identifier is the application's object ID. Mirrors `az ad app show`.

## EXAMPLES

### Example 1: Get an application by object ID

```powershell
Get-AzMsGraphApplication -Id 00000000-0000-0000-0000-000000000000
```

## PARAMETERS

### -Id

The object ID of the application registration to retrieve.

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

### Microsoft.Azure.Azclips.Extensions.Graph.Models.Application

## NOTES

## RELATED LINKS
