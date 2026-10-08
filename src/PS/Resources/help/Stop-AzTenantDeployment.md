---
document type: cmdlet
external help file: Microsoft.Azure.Azclips.Cmdlets.Resources.dll-Help.xml
HelpUri: ''
Locale: en-US
Module Name: Resources
ms.date: 10/08/2026
PlatyPS schema version: 2024-05-01
title: Stop-AzTenantDeployment
---

# Stop-AzTenantDeployment

## SYNOPSIS

Cancel a running tenant-scope deployment.

## SYNTAX

### StopByName (Default)

```
Stop-AzTenantDeployment [-Name] <string> [-PassThru] [-Profile <string>] [-WhatIf] [-Confirm]
 [<CommonParameters>]
```

### StopById

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

## DESCRIPTION

Cancels a tenant deployment only while it is active. The cmdlet accepts a name, tenant deployment resource ID, or deployment object.

## EXAMPLES

### Example 1

Cancel a running tenant deployment.

```powershell
Stop-AzTenantDeployment -Name tenant-rollout -PassThru
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

The full tenant deployment resource ID.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases:
- DeploymentId
- ResourceId
ParameterSets:
- Name: StopById
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

A tenant deployment object received from the pipeline.

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

The tenant deployment name.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases:
- DeploymentName
ParameterSets:
- Name: StopByName
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

Return True after the operation succeeds.

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

The saved AzClips profile to use.

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

Cancels a tenant deployment only while it is active. The cmdlet accepts a name, tenant deployment resource ID, or deployment object.

## OUTPUTS

### System.Boolean

Cancels a tenant deployment only while it is active. The cmdlet accepts a name, tenant deployment resource ID, or deployment object.

## NOTES

Tenant deployments use API version 2025-04-01 and Incremental mode.

## RELATED LINKS


- [Azure Resource Manager deployments](https://learn.microsoft.com/azure/azure-resource-manager/templates/deploy-what-if)
