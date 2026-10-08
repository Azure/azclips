---
document type: cmdlet
external help file: Microsoft.Azure.Azclips.Cmdlets.Resources.dll-Help.xml
HelpUri: ''
Locale: en-US
Module Name: Resources
ms.date: 10/08/2026
PlatyPS schema version: 2024-05-01
title: Get-AzTenantDeployment
---

# Get-AzTenantDeployment

## SYNOPSIS

Get one or more tenant-scope deployments.

## SYNTAX

### List (Default)

```
Get-AzTenantDeployment [-Profile <string>] [<CommonParameters>]
```

### GetByName

```
Get-AzTenantDeployment [-Name] <string> [-Profile <string>] [<CommonParameters>]
```

### GetById

```
Get-AzTenantDeployment -Id <string> [-Profile <string>] [<CommonParameters>]
```

## ALIASES

## DESCRIPTION

Gets a tenant deployment by name or resource ID. Omit both to list all tenant deployments; list output follows ARM pagination.

## EXAMPLES

### Example 1

List tenant deployments.

```powershell
Get-AzTenantDeployment
```

## PARAMETERS

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
- Name: GetById
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

The tenant deployment name.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases:
- DeploymentName
ParameterSets:
- Name: GetByName
  Position: 0
  IsRequired: true
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

### CommonParameters

This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable,
-InformationAction, -InformationVariable, -OutBuffer, -OutVariable, -PipelineVariable,
-ProgressAction, -Verbose, -WarningAction, and -WarningVariable. For more information, see
[about_CommonParameters](https://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

## OUTPUTS

### Microsoft.Azure.Azclips.Extensions.Resources.Models.DeploymentExtended

Gets a tenant deployment by name or resource ID. Omit both to list all tenant deployments; list output follows ARM pagination.

## NOTES

Tenant deployments use API version 2025-04-01 and Incremental mode.

## RELATED LINKS


- [Azure Resource Manager deployments](https://learn.microsoft.com/azure/azure-resource-manager/templates/deploy-what-if)
