---
document type: cmdlet
external help file: Microsoft.Azure.Azclips.Cmdlets.Resources.dll-Help.xml
HelpUri: ''
Locale: en-US
Module Name: Resources
ms.date: 10/07/2026
PlatyPS schema version: 2024-05-01
title: Get-AzTenantDeploymentOperation
---

# Get-AzTenantDeploymentOperation

## SYNOPSIS

Get the individual operations of a tenant scoped deployment.

## SYNTAX

### List (Default)

```
Get-AzTenantDeploymentOperation -DeploymentName <string> [-Profile <string>] [<CommonParameters>]
```

### Get

```
Get-AzTenantDeploymentOperation -DeploymentName <string> -OperationIds <string[]>
 [-Profile <string>] [<CommonParameters>]
```

## ALIASES

## DESCRIPTION

A deployment operation records one resource action taken during a deployment, including its provisioning operation and state, timestamp, duration, status code and message, target resource, and the captured HTTP request and response.
Omit `-OperationIds` to list every operation of the deployment; supply one or more ids to fetch just those.
This is the primary way to find the specific resource that caused a deployment to fail.

## EXAMPLES

### List the operations of a deployment

Return every operation recorded for the tenant scoped deployment `scenario-deployment`.

```powershell
Get-AzTenantDeploymentOperation -DeploymentName scenario-deployment
```

### Get specific operations by id

Fetch two operations by id. Each id is retrieved individually and the results are returned as one array.

```powershell
Get-AzTenantDeploymentOperation -DeploymentName scenario-deployment -OperationIds 38241FA9B6B8DAE1, 08584103205698517279
```

## PARAMETERS

### -DeploymentName

The deployment name.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases:
- Name
ParameterSets:
- Name: Get
  Position: Named
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: true
  ValueFromRemainingArguments: false
- Name: List
  Position: Named
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: true
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -OperationIds

One or more deployment operation Ids to get. Omit to list every operation of the deployment.

```yaml
Type: System.String[]
DefaultValue: ''
SupportsWildcards: false
Aliases:
- OperationId
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

### CommonParameters

This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable,
-InformationAction, -InformationVariable, -OutBuffer, -OutVariable, -PipelineVariable,
-ProgressAction, -Verbose, -WarningAction, and -WarningVariable. For more information, see
[about_CommonParameters](https://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

### System.String

The deployment name can be supplied from the pipeline by property name.

## OUTPUTS

### Microsoft.Azure.Azclips.Extensions.Resources.Models.DeploymentOperation

One `DeploymentOperation` object per matching operation — one per requested id when `-OperationIds` is supplied, every operation of the deployment otherwise.

## NOTES

Generated help for resources.

## RELATED LINKS

- [Online Version]()

