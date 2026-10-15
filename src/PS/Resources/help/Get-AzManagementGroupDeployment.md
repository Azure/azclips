---
document type: cmdlet
external help file: Microsoft.Azure.Azclips.Cmdlets.Resources.dll-Help.xml
HelpUri: ''
Locale: en-US
Module Name: Resources
ms.date: 10/15/2026
PlatyPS schema version: 2024-05-01
title: Get-AzManagementGroupDeployment
---

# Get-AzManagementGroupDeployment

## SYNOPSIS

Gets management-group deployments.

## SYNTAX

### GetByName (Default)

```
Get-AzManagementGroupDeployment [-ManagementGroupId] <string> [[-Name] <string>] [-Pre]
 [-Profile <string>] [<CommonParameters>]
```

### GetById

```
Get-AzManagementGroupDeployment -Id <string> [-Pre] [-Profile <string>] [<CommonParameters>]
```

### GetByDeploymentName (Default)

```
Get-AzManagementGroupDeployment [-ManagementGroupId] <string> [[-Name] <string>] [-Profile <string>]
 [<CommonParameters>]
```

### GetByDeploymentId

```
Get-AzManagementGroupDeployment -Id <string> [-Profile <string>] [<CommonParameters>]
```

## ALIASES

This cmdlet has no aliases.


## DESCRIPTION

Lists deployments when only `-ManagementGroupId` is supplied. Gets one deployment when
`-Name` or a full deployment `-Id` is supplied.

## EXAMPLES

### Example 1

Lists all deployments recorded at a management group.

```powershell
Get-AzManagementGroupDeployment -ManagementGroupId myManagementGroup
```

### Example 2

Gets one deployment by full resource ID.

```powershell
$id = '/providers/Microsoft.Management/managementGroups/myManagementGroup/providers/Microsoft.Resources/deployments/myDeployment'
Get-AzManagementGroupDeployment -Id $id
```

## PARAMETERS

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
- Name: GetByDeploymentId
  Position: Named
  IsRequired: true
  ValueFromPipeline: false
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
- Name: GetByDeploymentName
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

The deployment name. Omit it to list deployments.
The name of deployment.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases:
- DeploymentName
ParameterSets:
- Name: GetByDeploymentName
  Position: 1
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

### CommonParameters

This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable,
-InformationAction, -InformationVariable, -OutBuffer, -OutVariable, -PipelineVariable,
-ProgressAction, -Verbose, -WarningAction, and -WarningVariable. For more information, see
[about_CommonParameters](https://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

## OUTPUTS

### Microsoft.Azure.Azclips.Extensions.Resources.Models.DeploymentExtended

Returns one or more management-group deployment objects.

## NOTES

The full ID form must identify a `Microsoft.Resources/deployments` resource beneath a
`Microsoft.Management/managementGroups` scope.

## RELATED LINKS


- [Azure Resource Manager deployments](https://learn.microsoft.com/azure/azure-resource-manager/templates/deploy-to-management-group)
