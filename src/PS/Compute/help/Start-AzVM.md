---
document type: cmdlet
external help file: Microsoft.Azure.Azclips.Cmdlets.Compute.dll-Help.xml
HelpUri: ''
Locale: en-US
Module Name: Compute
ms.date: 09/07/2026
PlatyPS schema version: 2024-05-01
title: Start-AzVM
---

# Start-AzVM

## SYNOPSIS

Start a virtual machine.

## SYNTAX

### ResourceGroupNameParameterSetName (Default)

```
Start-AzVM [-ResourceGroupName] <string> [-Name] <string> [-SubscriptionId <string>] [-NoWait]
 [-Profile <string>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### IdParameterSetName

```
Start-AzVM [-Id] <string> [-SubscriptionId <string>] [-NoWait] [-Profile <string>] [-WhatIf]
 [-Confirm] [<CommonParameters>]
```

## ALIASES

## DESCRIPTION

Starts the specified virtual machine.
Identify the virtual machine either by `-Name` together with `-ResourceGroupName`, or by its complete resource ID with `-Id`.

The operation is long running.
By default the cmdlet waits until the platform reports that the virtual machine has finished starting; supply `-NoWait` to return as soon as Azure accepts the request.
The cmdlet does not emit an object on success.
Starting an already-running virtual machine is a no-op.

## EXAMPLES

### Start a VM by name

Start the virtual machine `myvm` in resource group `myrg`.

```powershell
Start-AzVM -ResourceGroupName myrg -Name myvm
```

### Start a VM by resource ID

```powershell
Start-AzVM -Id /subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/myrg/providers/Microsoft.Compute/virtualMachines/myvm
```

### Start a VM without waiting for it to finish

```powershell
Start-AzVM -ResourceGroupName myrg -Name myvm -NoWait
```

### Start every VM in a resource group from the pipeline

`Get-AzVM` emits objects carrying an `Id` property, which binds to `-Id` by property name.

```powershell
Get-AzVM -ResourceGroupName myrg | Start-AzVM -NoWait
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

### -Id

The complete resource ID of the virtual machine.
Supplies the subscription, resource group and name, so it replaces `-Name` and `-ResourceGroupName`.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: IdParameterSetName
  Position: 0
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: true
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -Name

The name of the virtual machine.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: ResourceGroupNameParameterSetName
  Position: 1
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: true
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -NoWait

Starts the operation and returns immediately, before the operation is completed.
In order to determine if the operation has successfully been completed, use some other mechanism.

```yaml
Type: System.Management.Automation.SwitchParameter
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

### -ResourceGroupName

The resource group name.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: ResourceGroupNameParameterSetName
  Position: 0
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: true
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -SubscriptionId

Azure Subscription Id. Defaults to the active profile's subscription.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: ResourceGroupNameParameterSetName
  Position: Named
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
- Name: IdParameterSetName
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

Shows what would happen if the cmdlet runs.
The cmdlet is not run.
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

## OUTPUTS

### System.Void

This cmdlet does not emit an object on the pipeline; it only writes progress and status messages via the Information and Verbose streams.

## NOTES

Generated help for compute.

## RELATED LINKS


- [Online Version]()
