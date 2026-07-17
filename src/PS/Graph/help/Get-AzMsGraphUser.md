---
document type: cmdlet
external help file: Microsoft.Azure.Azclips.Cmdlets.Graph.dll-Help.xml
HelpUri: ''
Locale: en-US
Module Name: Graph
ms.date: 07/17/2026
PlatyPS schema version: 2024-05-01
title: Get-AzMsGraphUser
---

# Get-AzMsGraphUser

## SYNOPSIS

Gets a Microsoft Graph user by object ID or user principal name.

## SYNTAX

### __AllParameterSets

```
Get-AzMsGraphUser -Id <string> [<CommonParameters>]
```

## ALIASES

## DESCRIPTION

Retrieves a user object from Microsoft Graph using either the user's object ID or user principal name (UPN). The active Azure profile (see `Connect-AzAccount`) is used to authenticate to Microsoft Graph.

## EXAMPLES

### Example 1

Gets a user's Microsoft Graph profile by user principal name.

```powershell
Get-AzMsGraphUser -Id user@contoso.com
```

### Example 2

Gets a user's Microsoft Graph profile by object ID.

```powershell
Get-AzMsGraphUser -Id 00000000-0000-0000-0000-000000000000
```

## PARAMETERS

### -Id

The object ID or user principal name (UPN) of the Microsoft Graph user to retrieve.

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

### CommonParameters

This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable,
-InformationAction, -InformationVariable, -OutBuffer, -OutVariable, -PipelineVariable,
-ProgressAction, -Verbose, -WarningAction, and -WarningVariable. For more information, see
[about_CommonParameters](https://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

## OUTPUTS

### System.Object

The Microsoft Graph user object returned by the Microsoft Graph `/users/{id}` endpoint.

## NOTES

## RELATED LINKS
