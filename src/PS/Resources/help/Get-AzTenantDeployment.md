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

### __AllParameterSets

```
Get-AzTenantDeployment [[-Name] <string>] [-Profile <string>] [<CommonParameters>]
```

## ALIASES

This cmdlet has no aliases.

## DESCRIPTION

Gets a deployment at the current tenant scope by name, mirroring legacy `Get-AzTenantDeployment`.
Omit `-Name` to list all deployments at tenant scope.

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

## PARAMETERS

### -Name

The name of deployment.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: (All)
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

### Microsoft.Azure.Azclips.Extensions.Resources.Models.DeploymentExtended

Returns the tenant deployment or deployments.

## NOTES

Tenant deployments use API version 2025-04-01.

## RELATED LINKS

- [Azure Resource Manager tenant deployments](https://learn.microsoft.com/azure/azure-resource-manager/templates/deploy-to-tenant)
