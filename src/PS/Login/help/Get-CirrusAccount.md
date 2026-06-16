---
document type: cmdlet
external help file: Microsoft.Azure.Cirrus.Cmdlets.Login.dll-Help.xml
HelpUri: ""
Locale: en-US
Module Name: Login
ms.date: 06/16/2026
PlatyPS schema version: 2024-05-01
title: Get-CirrusAccount
---

# Get-CirrusAccount

## SYNOPSIS

Show the current Azure account context.

## SYNTAX

### \_\_AllParameterSets

```
Get-CirrusAccount [<CommonParameters>]
```

## ALIASES

This cmdlet has the following aliases,
{{Insert list of aliases}}

## DESCRIPTION

Show the current Azure account context.

## EXAMPLES

### Show the current account context

Returns the persisted account information — subscription, tenant, and signed-in user — from the most recent `Connect-CirrusAccount` call. Returns `$null` when no account is connected.

```powershell
Get-CirrusAccount
```

## PARAMETERS

### CommonParameters

This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable,
-InformationAction, -InformationVariable, -OutBuffer, -OutVariable, -PipelineVariable,
-ProgressAction, -Verbose, -WarningAction, and -WarningVariable. For more information, see
[about_CommonParameters](https://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

## OUTPUTS

## NOTES

Generated help for login.

## RELATED LINKS

- [Online Version]()
