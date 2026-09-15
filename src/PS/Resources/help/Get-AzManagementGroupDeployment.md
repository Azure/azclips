---
document type: cmdlet
external help file: Microsoft.Azure.Azclips.Cmdlets.Resources.dll-Help.xml
HelpUri: ''
Locale: en-US
Module Name: Resources
ms.date: 09/15/2026
PlatyPS schema version: 2024-05-01
title: Get-AzManagementGroupDeployment
---

# Get-AzManagementGroupDeployment

## SYNOPSIS

Gets management-group-scoped deployments.

## SYNTAX

### List (Default)

```
Get-AzManagementGroupDeployment [-ManagementGroupId] <string> [-Profile <string>]
 [<CommonParameters>]
```

### Get

```
Get-AzManagementGroupDeployment [-ManagementGroupId] <string> [-Name] <string> [-Profile <string>]
 [<CommonParameters>]
```

### Id

```
Get-AzManagementGroupDeployment -Id <string> [-Profile <string>] [<CommonParameters>]
```

## ALIASES

This cmdlet has no aliases.

## DESCRIPTION

Lists deployments when Name is omitted, or gets one deployment by name or resource ID.

## EXAMPLES

### List deployments

Lists all deployments at the specified management-group scope.

```powershell
Get-AzManagementGroupDeployment -ManagementGroupId myManagementGroup
```

### Get a deployment by name

Gets a deployment by its management group and deployment name.

```powershell
Get-AzManagementGroupDeployment -ManagementGroupId myManagementGroup -Name myDeployment
```

## PARAMETERS

### -Id

The fully qualified management-group deployment resource ID.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases:
- DeploymentId
- ResourceId
ParameterSets:
- Name: Id
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
- Name: Get
  Position: 0
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
- Name: List
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

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases:
- DeploymentName
ParameterSets:
- Name: Get
  Position: 1
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

## INPUTS

None.

## OUTPUTS

### Microsoft.Azure.Azclips.Extensions.Resources.Models.DeploymentExtended

Returns the requested deployment or deployments.

## NOTES

## RELATED LINKS

This cmdlet has no related links.
