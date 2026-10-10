---
document type: cmdlet
external help file: Microsoft.Azure.Azclips.Cmdlets.Resources.dll-Help.xml
HelpUri: ''
Locale: en-US
Module Name: Resources
ms.date: 10/10/2026
PlatyPS schema version: 2024-05-01
title: Get-AzTenantDeployment
---

# Get-AzTenantDeployment

## SYNOPSIS

Get deployments at tenant scope.

## SYNTAX

### GetByDeploymentName (Default)

```
Get-AzTenantDeployment [[-Name] <string>] [-Profile <string>] [<CommonParameters>]
```

### GetByDeploymentId

```
Get-AzTenantDeployment -Id <string> [-Profile <string>] [<CommonParameters>]
```

## ALIASES

This cmdlet has no aliases.

## DESCRIPTION

Gets a deployment at the current tenant scope by name or resource ID, mirroring legacy `Get-AzTenantDeployment`.
Omit `-Name` and `-Id` to list all deployments at tenant scope.

## EXAMPLES

### Example 1: Get all tenant deployments

List every deployment at the current tenant scope.

```powershell
Get-AzTenantDeployment
```

### Example 2: Get a tenant deployment by name

Get a single deployment at tenant scope.

```powershell
Get-AzTenantDeployment -Name tenantBaseline
```

### Example 3: Get a tenant deployment by resource ID

Get a deployment by its fully qualified resource ID.

```powershell
Get-AzTenantDeployment -Id /providers/Microsoft.Resources/deployments/tenantBaseline
```

## PARAMETERS

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

### -Name

The name of deployment.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases:
- DeploymentName
ParameterSets:
- Name: GetByDeploymentName
  Position: 0
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

### CommonParameters

This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable,
-InformationAction, -InformationVariable, -OutBuffer, -OutVariable, -PipelineVariable,
-ProgressAction, -Verbose, -WarningAction, and -WarningVariable. For more information, see
[about_CommonParameters](https://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

## OUTPUTS

### Microsoft.Azure.Azclips.Extensions.Resources.Processors.PSDeployment

Returns the tenant deployment or deployments.

## NOTES

Tenant deployments use API version 2025-04-01.

## RELATED LINKS

- [Azure Resource Manager tenant deployments](https://learn.microsoft.com/azure/azure-resource-manager/templates/deploy-to-tenant)
