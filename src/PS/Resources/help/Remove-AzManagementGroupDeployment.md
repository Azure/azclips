---
document type: cmdlet
external help file: Microsoft.Azure.Azclips.Cmdlets.Resources.dll-Help.xml
HelpUri: ''
Locale: en-US
Module Name: Resources
ms.date: 10/12/2026
PlatyPS schema version: 2024-05-01
title: Remove-AzManagementGroupDeployment
---

# Remove-AzManagementGroupDeployment

## SYNOPSIS

Removes a management-group deployment from deployment history.

## SYNTAX

### RemoveByName (Default)

```
Remove-AzManagementGroupDeployment [-ManagementGroupId] <string> [-Name] <string> [-PassThru]
 [-AsJob] [-Pre] [-Profile <string>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### RemoveById

```
Remove-AzManagementGroupDeployment -Id <string> [-PassThru] [-AsJob] [-Pre] [-Profile <string>]
 [-WhatIf] [-Confirm] [<CommonParameters>]
```

### RemoveByInputObject

```
Remove-AzManagementGroupDeployment -InputObject <PSDeployment> [-AsJob] [-PassThru]
 [-Profile <string>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### RemoveByDeploymentName (Default)

```
Remove-AzManagementGroupDeployment [-ManagementGroupId] <string> [-Name] <string> [-AsJob]
 [-PassThru] [-Profile <string>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### RemoveByDeploymentId

```
Remove-AzManagementGroupDeployment -Id <string> [-AsJob] [-PassThru] [-Profile <string>] [-WhatIf]
 [-Confirm] [<CommonParameters>]
```

## ALIASES

This cmdlet has no aliases.


## DESCRIPTION

Deletes deployment history and associated deployment operations. The target can be supplied
by management group and name, full deployment ID, or pipeline input object.

## EXAMPLES

### Example 1

Removes deployment history by management group and name.

```powershell
Remove-AzManagementGroupDeployment -ManagementGroupId myManagementGroup -Name myDeployment -Confirm:$false
```

### Example 2

Removes deployment history supplied through the pipeline and returns `true` on success.

```powershell
Get-AzManagementGroupDeployment -ManagementGroupId myManagementGroup -Name myDeployment |
    Remove-AzManagementGroupDeployment -PassThru -Confirm:$false
```

## PARAMETERS

### -AsJob

Runs the cmdlet in the background and returns a job object.
Run cmdlet in the background

```yaml
Type: System.Management.Automation.SwitchParameter
DefaultValue: False
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

The full management-group deployment resource ID.
The fully qualified resource Id of the deployment. example: /providers/Microsoft.Management/managementGroups/{managementGroupId}/providers/Microsoft.Resources/deployments/{deploymentName}

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases:
- DeploymentId
- ResourceId
ParameterSets:
- Name: RemoveByDeploymentId
  Position: Named
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -InputObject

A deployment object returned by `Get-AzManagementGroupDeployment`.
The deployment object.

```yaml
Type: Microsoft.Azure.Azclips.Extensions.Resources.Processors.PSDeployment
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: RemoveByInputObject
  Position: Named
  IsRequired: true
  ValueFromPipeline: true
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -ManagementGroupId

The management group ID.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: RemoveByDeploymentName
  Position: 0
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -Name

The deployment name.
The name of the deployment.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases:
- DeploymentName
ParameterSets:
- Name: RemoveByDeploymentName
  Position: 1
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -PassThru

Returns `true` when deletion succeeds.

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

### -Pre

Accepted for compatibility with Azure PowerShell. Deployment cmdlets use a fixed API version, so this switch has no effect.

```yaml
Type: System.Management.Automation.SwitchParameter
DefaultValue: False
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

The AzClips profile to use instead of the active profile.

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

### Microsoft.Azure.Azclips.Extensions.Resources.Processors.PSDeployment

Accepts a management-group deployment object from the pipeline.

## OUTPUTS

### System.Boolean

Returns `true` when `-PassThru` is specified and deletion succeeds.

## NOTES

Removing deployment history does not delete resources created by the deployment.

## RELATED LINKS


- [Azure Resource Manager deployments](https://learn.microsoft.com/azure/azure-resource-manager/templates/deploy-to-management-group)
