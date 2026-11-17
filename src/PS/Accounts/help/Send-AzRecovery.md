---
document type: cmdlet
external help file: Microsoft.Azure.Azclips.Cmdlets.Accounts.dll-Help.xml
HelpUri: ''
Locale: en-US
Module Name: Accounts
ms.date: 11/17/2026
PlatyPS schema version: 2024-05-01
title: Send-AzRecovery
---

# Send-AzRecovery

## SYNOPSIS

Sends an AzCLIPS PowerShell error to Azure AI for recovery advice.

## SYNTAX

### __AllParameterSets

```
Send-AzRecovery [-Id <string>] [-Style <string>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

## ALIASES

## DESCRIPTION

Sends the failed cmdlet's name and error text to the built-in Azure AI provider.
By default, it selects the latest AzCLIPS cmdlet error in the current PowerShell session.
Suggested commands are not run automatically.

Sign in with `Connect-AzAccount`, use the `builtin` provider, and enable recovery
with `Set-AzConfig -Key agent.recovery.trigger -Value manual`.

## EXAMPLES

### Get advice for the latest error

Returns one recovery response without prompting for follow-up input.

```powershell
Send-AzRecovery -Style oneshot
```

### Preview recovery for a selected error

Use `Get-AzRecoveryId` to find the ID. `-WhatIf` reports which error would be sent
without contacting Azure AI.

```powershell
Send-AzRecovery -Id '48212-cfd9ddb4' -WhatIf
```

## PARAMETERS

### -Confirm

Prompts for confirmation before sending the error.

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

### -Id

Selects a current-session error by its recovery ID. Use `Get-AzRecoveryId` to list IDs.
If omitted, the cmdlet selects the latest eligible error.

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

### -Style

Overrides `agent.recovery.style` for this invocation. `oneshot` returns one response.
`interactive` accepts follow-up input; press Enter at the empty prompt to finish.

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
AcceptedValues:
- oneshot
- interactive
HelpMessage: ''
```

### -WhatIf

Shows which error would be sent without sending it.

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

### System.String

Recovery advice as text.

## NOTES

## RELATED LINKS

[Get-AzRecoveryId](Get-AzRecoveryId.md)

[Connect-AzAccount](Connect-AzAccount.md)
