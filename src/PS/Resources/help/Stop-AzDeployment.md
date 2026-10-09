---
document type: cmdlet
external help file: Microsoft.Azure.Azclips.Cmdlets.Resources.dll-Help.xml
HelpUri: ''
Locale: en-US
Module Name: Resources
ms.date: 10-09-2026
PlatyPS schema version: 2024-05-01
title: Stop-AzDeployment
---

# Stop-AzDeployment

## SYNOPSIS

Cancel a currently running subscription-scope deployment.

## SYNTAX

### __AllParameterSets

```
Stop-AzDeployment [-Name] <string> [-SubscriptionId <string>] [-Profile <string>] [-WhatIf]
 [-Confirm] [<CommonParameters>]
```

## ALIASES

This cmdlet has the following aliases,
  Stop-AzSubscriptionDeployment

## DESCRIPTION

Cancels a running Azure Resource Manager deployment scoped to a subscription. A deployment can only be cancelled while its provisioning state is `Accepted` or `Running`; once cancelled the state becomes `Canceled`, and any resources the deployment already created remain. Cancelling a deployment that has already reached a terminal state fails. The cmdlet emits nothing on success.

## EXAMPLES

### Cancel a running deployment

Stop the in-flight deployment named `sub-infra-2026-06-18`.

```powershell
Stop-AzDeployment -Name sub-infra-2026-06-18
```

### Cancel a deployment in a specific subscription without being prompted

```powershell
Stop-AzDeployment -Name sub-infra-2026-06-18 -SubscriptionId 00000000-0000-0000-0000-000000000000 -Confirm:$false
```

## PARAMETERS

### -Confirm

Prompts you for confirmation before running the cmdlet.

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

### -Name

The name of the deployment to cancel.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases:
- DeploymentName
ParameterSets:
- Name: (All)
  Position: 0
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: true
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -Profile

Name of a saved profile to use for this call instead of the currently-active one. Manage profiles with `Set-AzProfile` / `Get-AzProfile`.

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

### -SubscriptionId

The Azure subscription that owns the deployment. Defaults to the active profile's subscription when omitted.

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

### -WhatIf

Runs the command in a mode that only reports what would happen without performing the actions.

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

### System.String

You can pipe the **Name** value to this cmdlet by property name, for example the output of `Get-AzDeployment`.

## OUTPUTS

### System.Void

Nothing is emitted on success. Failures surface as terminating errors.

## NOTES

Cancelling does not roll back resources the deployment already created. Use `Get-AzDeployment` to confirm the deployment reached the `Canceled` state. The legacy Az.Resources name `Stop-AzSubscriptionDeployment` is accepted as an alias.

## RELATED LINKS

- [Get-AzDeployment]()
- [New-AzDeployment]()
- [Remove-AzDeployment]()
