---
document type: cmdlet
external help file: Microsoft.Azure.Azclips.Cmdlets.Compute.dll-Help.xml
HelpUri: ''
Locale: en-US
Module Name: Compute
ms.date: 09/23/2026
PlatyPS schema version: 2024-05-01
title: Get-AzVM
---

# Get-AzVM

## SYNOPSIS

Show details for a virtual machine.

## SYNTAX

### List (Default)

```
Get-AzVM -ResourceGroupName <string> [-SubscriptionId <string>] [-Profile <string>]
 [<CommonParameters>]
```

### Get

```
Get-AzVM -ResourceGroupName <string> -Name <string> [-SubscriptionId <string>] [-UserData]
 [-Profile <string>] [<CommonParameters>]
```

### Id

```
Get-AzVM [-Id] <string> [-SubscriptionId <string>] [-UserData] [-Profile <string>]
 [<CommonParameters>]
```

## ALIASES

## DESCRIPTION

Retrieves details of virtual machines in a subscription.
Supply `-ResourceGroupName` together with `-Name` to retrieve a single virtual machine, or `-ResourceGroupName` alone to list every virtual machine in that resource group.
Alternatively, address a single virtual machine directly by its ARM resource ID with `-Id`.

## EXAMPLES

### Get details of a specific VM

Retrieve metadata for the virtual machine `myvm` in resource group `myrg`.

```powershell
Get-AzVM -SubscriptionId 00000000-0000-0000-0000-000000000000 -ResourceGroupName myrg -Name myvm
```

### List all VMs in a resource group

Return every virtual machine that lives in `myrg`.

```powershell
Get-AzVM -SubscriptionId 00000000-0000-0000-0000-000000000000 -ResourceGroupName myrg
```

### Get a VM by resource ID

Retrieve a virtual machine addressed by its ARM resource ID.

```powershell
Get-AzVM -Id /subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/myrg/providers/Microsoft.Compute/virtualMachines/myvm
```

## PARAMETERS

### -Id

The resource ID of the virtual machine. Provide either -Id or -Name and -ResourceGroupName.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: Id
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
- Name: Get
  Position: Named
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
- Name: List
  Position: Named
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
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
- Name: Get
  Position: Named
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
- Name: List
  Position: Named
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
- Name: Id
  Position: Named
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -UserData

Include the user data properties in the query result.

```yaml
Type: System.Management.Automation.SwitchParameter
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: Get
  Position: Named
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
- Name: Id
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

You can pipe a virtual machine resource ID to this cmdlet by property name.

## OUTPUTS

### Microsoft.Azure.Azclips.Extensions.Compute.Models.VirtualMachine

One `VirtualMachine` object per matching VM — a single result when `-Name` is supplied (Get mode), and an array of results in List mode or when addressing by `-Id` (Id mode). Each includes the ARM resource ID, provisioning state, VM size, and network configuration.

## NOTES

Generated help for compute.

## RELATED LINKS


- [Online Version]()
