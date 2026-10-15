---
document type: cmdlet
external help file: Microsoft.Azure.Azclips.Cmdlets.Resources.dll-Help.xml
HelpUri: ''
Locale: en-US
Module Name: Resources
ms.date: 10/10/2026
PlatyPS schema version: 2024-05-01
title: Stop-AzTenantDeployment
---

# Stop-AzTenantDeployment

## SYNOPSIS

Cancel a running deployment at tenant scope.

## SYNTAX

### StopByDeploymentName (Default)

```
Stop-AzTenantDeployment [-Name] <string> [-PassThru] [-Profile <string>] [-WhatIf] [-Confirm]
 [<CommonParameters>]
```

### StopByDeploymentId

```
Stop-AzTenantDeployment -Id <string> [-PassThru] [-Profile <string>] [-WhatIf] [-Confirm]
 [<CommonParameters>]
```

### StopByInputObject

```
Stop-AzTenantDeployment -InputObject <DeploymentExtended> [-PassThru] [-Profile <string>] [-WhatIf]
 [-Confirm] [<CommonParameters>]
```

## ALIASES

This cmdlet has no aliases.

## DESCRIPTION

Cancels a deployment at the current tenant scope that has started but not completed, mirroring legacy `Stop-AzTenantDeployment`.
Specify the deployment by name, resource ID, or a deployment object from `Get-AzTenantDeployment`.
The cmdlet fails if the deployment has already succeeded or failed.

## EXAMPLES

### Example 1: Cancel a tenant deployment by name

Cancel a running deployment at tenant scope.

```powershell
Stop-AzTenantDeployment -Name tenantRollout
```

### Example 2: Cancel a tenant deployment from the pipeline

Cancel a deployment returned by Get-AzTenantDeployment and return True.

```powershell
Get-AzTenantDeployment -Name tenantRollout | Stop-AzTenantDeployment -PassThru
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

The fully qualified resource Id of the deployment. example: /providers/Microsoft.Resources/deployments/{deploymentName}

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases:
- DeploymentId
- ResourceId
ParameterSets:
- Name: StopByDeploymentId
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

The deployment object.

```yaml
Type: Microsoft.Azure.Azclips.Extensions.Resources.Models.DeploymentExtended
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: StopByInputObject
  Position: Named
  IsRequired: true
  ValueFromPipeline: true
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -Name

The name of the deployment.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases:
- DeploymentName
ParameterSets:
- Name: StopByDeploymentName
  Position: 0
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -PassThru

Returns `True` when the operation is requested. By default, this cmdlet does not generate any output.

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

Specifies a saved profile to use instead of the active profile.

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

### Microsoft.Azure.Azclips.Extensions.Resources.Models.DeploymentExtended

You can pipe a tenant deployment object to this cmdlet.

## OUTPUTS

### System.Boolean

Returns `True` when `-PassThru` is specified. By default, this cmdlet does not generate any output.

## NOTES

Only deployments in a running state (for example Accepted or Running) can be canceled.

## RELATED LINKS

- [Azure Resource Manager tenant deployments](https://learn.microsoft.com/azure/azure-resource-manager/templates/deploy-to-tenant)
