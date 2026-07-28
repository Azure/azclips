---
document type: cmdlet
external help file: Microsoft.Azure.Azclips.Cmdlets.MsGraph.dll-Help.xml
HelpUri: ''
Locale: en-US
Module Name: Graph
ms.date: 07/20/2026
PlatyPS schema version: 2024-05-01
title: Get-AzMsGraphApplication
---

# Get-AzMsGraphApplication

## SYNOPSIS

Gets Microsoft Graph application registrations. Lists registrations in the tenant, or retrieves a single one by object ID.

## SYNTAX

### List (Default)

```
Get-AzMsGraphApplication [-Filter <string>] [-Orderby <string[]>] [-Search <string>]
 [-ConsistencyLevel <string>] [-Select <string[]>] [<CommonParameters>]
```

### DisplayName

```
Get-AzMsGraphApplication -DisplayName <string> [-Select <string[]>] [<CommonParameters>]
```

### Get

```
Get-AzMsGraphApplication -Id <string> [<CommonParameters>]
```

## ALIASES

## DESCRIPTION

Queries Microsoft Graph for application registrations using the active Azure profile (see `Connect-AzAccount`). The available parameter sets are:

- **List (Default)**: returns the first page of applications. Combine with `-Filter`, `-Orderby`, `-Search`, and `-ConsistencyLevel` for raw OData queries. Pagination is not followed in this preview.
- **DisplayName**: shortcut that translates to the OData filter `displayName eq '<value>'`.
- **Get**: retrieves a single application by object ID.

`-Select` controls OData projection and is available in the list-based parameter sets.

## EXAMPLES

### Example 1: List the first page of applications

```powershell
Get-AzMsGraphApplication
```

### Example 2: List applications by exact display name

```powershell
Get-AzMsGraphApplication -DisplayName my-app
```

### Example 3: Get an application by object ID

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

### -DisplayName

Exact display name to match. Translated to the OData filter `displayName eq '<value>'`.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: DisplayName
  Position: Named
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -Filter

OData `$filter` expression applied by Microsoft Graph. Passed through verbatim.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
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

### -Select

Properties to project via OData `$select`.

```yaml
Type: System.String[]
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: List
  Position: Named
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
- Name: DisplayName
  Position: Named
  IsRequired: false
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

### Microsoft.Azure.Azclips.Extensions.MsGraph.Models.Application

## NOTES

## RELATED LINKS
