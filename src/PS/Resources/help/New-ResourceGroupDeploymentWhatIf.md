---
document type: cmdlet
external help file: Microsoft.Azure.Cirrus.Cmdlets.Resources.dll-Help.xml
HelpUri: ''
Locale: en-US
Module Name: Resources
ms.date: 06/18/2026
PlatyPS schema version: 2024-05-01
title: New-ResourceGroupDeploymentWhatIf
---

# New-ResourceGroupDeploymentWhatIf

## SYNOPSIS

Preview the impact of a resource group deployment without creating or modifying resources.

## SYNTAX

### __AllParameterSets

```
New-ResourceGroupDeploymentWhatIf -ResourceGroupName <string> -Name <string>
 [-SubscriptionId <string>] [-TemplateUri <string>] [-TemplateFile <string>]
 [-TemplateSpecId <string>] [-QueryString <string>] [-ParametersUri <string>]
 [-ParametersFile <string>] [-Mode <string>] [-Parameter <string[]>] [-ValidationLevel <string>]
 [-ResultFormat <string>] [<CommonParameters>]
```

## ALIASES

This cmdlet has the following aliases,
  {{Insert list of aliases}}

## DESCRIPTION

Performs a what-if analysis on a resource group deployment to preview changes before applying them. This read-only operation validates the deployment and shows what resources would be created, updated, or deleted without making any actual changes.

## EXAMPLES

### Example 1

Preview a deployment using a local template file and parameters file.

```powershell
New-ResourceGroupDeploymentWhatIf -ResourceGroupName "myResourceGroup" -Name "myDeployment" -TemplateFile ".\template.json" -ParametersFile ".\parameters.json"
```

## PARAMETERS

### -Mode

Specifies the deployment mode (e.g., 'Incremental' or 'Complete'). Incremental deployments merge template resources with existing resources; Complete deployments replace all resources in the resource group.

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

### -Name

The name of the deployment. Must be unique within the resource group.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: (All)
  Position: Named
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -Parameter

Template parameter overrides as key-value pairs. Format: 'key1=value1' 'key2=value2'.

```yaml
Type: System.String[]
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

### -ParametersFile

The path to a local parameters JSON file containing deployment parameter values.

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

### -ParametersUri

The URI to a parameters JSON file in cloud storage (e.g., Azure Blob Storage) containing deployment parameter values.

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

### -QueryString

Query string parameters to append to template or parameters URIs for authentication or resource identification.

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

The name of the resource group where the deployment would be applied.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: (All)
  Position: Named
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -ResultFormat

The format for what-if results (e.g., 'ResourceIdOnly', 'FullResourcePayloads'). Controls the level of detail in the what-if output.

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

### -SubscriptionId

The Azure subscription ID. If not specified, uses the current subscription context.

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

### -TemplateFile

The path to a local ARM template JSON file defining the resources to deploy.

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

### -TemplateSpecId

The resource ID of a template spec to use for the deployment.

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

### -TemplateUri

The URI to an ARM template JSON file in cloud storage (e.g., Azure Blob Storage).

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

### -ValidationLevel

The level of template validation to perform (e.g., 'Basic', 'Full'). Higher levels perform more comprehensive validation checks.

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

### System.Object

What-if operation results containing details about resource changes (creates, updates, deletions) that would occur if the deployment were applied.

## NOTES

The what-if operation is read-only and does not modify any resources. Always review what-if results before running the actual deployment with New-ResourceGroupDeployment.

## RELATED LINKS

- [New-ResourceGroupDeployment](New-ResourceGroupDeployment.md)
- [Get-ResourceGroup](Get-ResourceGroup.md)

