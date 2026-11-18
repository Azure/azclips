---
document type: cmdlet
external help file: Microsoft.Azure.Azclips.Cmdlets.Accounts.dll-Help.xml
HelpUri: ''
Locale: en-US
Module Name: Accounts
ms.date: 11/17/2026
PlatyPS schema version: 2024-05-01
title: Get-AzRecoveryId
---

# Get-AzRecoveryId

## SYNOPSIS

Lists AzCLIPS PowerShell errors and their recovery IDs.

## SYNTAX

### __AllParameterSets

```
Get-AzRecoveryId [<CommonParameters>]
```

## ALIASES

## DESCRIPTION

Lists errors from AzCLIPS cmdlets in the current PowerShell session, newest first.
Each result includes an `Id` and the original PowerShell `Error` record. An ID remains
valid while its error is in `$Error`, unless the recovery trigger is set to `off` or
reset through AzCLIPS configuration in the PowerShell process. This invalidates
issued IDs without clearing `$Error`. CLI recovery IDs cannot be used here.

This cmdlet does not send error details to Azure AI.

## EXAMPLES

### List recoverable errors

Run this command after an AzCLIPS cmdlet fails to find its recovery ID.

```powershell
Get-AzRecoveryId
```

## PARAMETERS

### CommonParameters

This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable,
-InformationAction, -InformationVariable, -OutBuffer, -OutVariable, -PipelineVariable,
-ProgressAction, -Verbose, -WarningAction, and -WarningVariable. For more information, see
[about_CommonParameters](https://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

## OUTPUTS

### Microsoft.Azure.Azclips.Cmdlets.Accounts.RecoveryError

One object per eligible error, containing its recovery ID and original error record.

## NOTES

## RELATED LINKS

[Send-AzRecovery](Send-AzRecovery.md)
