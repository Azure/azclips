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

Sends the failed cmdlet's name, error category, recognized parameter names, HTTP
status when available, and, for binding failures, a general
description of the binding problem and possible parameter names to the built-in
Azure AI provider. The initial request omits raw error text, URLs, argument values,
and script text. Use `Get-AzRecoveryId` to inspect the original error locally.
By default, it selects the latest AzCLIPS cmdlet error in the current PowerShell session.
Suggested commands are not run automatically.

Sign in with `Connect-AzAccount` and use the `builtin` provider. Install and import
the AzCLIPS `Az.Config` module (separate from Accounts), then enable recovery with
`Set-AzConfig -Key agent.recovery.trigger -Value manual`.

Interactive recovery previews the diagnostic and asks before sending it. Follow-ups
are sent as typed, without redaction, and previous turns are included in each
subsequent request. Do not include passwords, tokens, sensitive URLs, or other
private data in follow-ups.

Azure AI error messages displayed by this cmdlet are not redacted and may contain
sensitive information.

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

Prompts for confirmation before sending the diagnostic.

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
`interactive` displays each response in the host as well as returning it to the
pipeline. Press Enter at the empty follow-up prompt to finish. Use `oneshot` in
noninteractive hosts.

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

Shows which error would be used without sending a diagnostic.

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
