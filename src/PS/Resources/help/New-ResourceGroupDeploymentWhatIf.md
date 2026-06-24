---
document type: cmdlet
external help file: Microsoft.Azure.Cirrus.Cmdlets.Resources.dll-Help.xml
HelpUri: ""
Locale: en-US
Module Name: Resources
ms.date: 06/18/2026
PlatyPS schema version: 2024-05-01
title: New-ResourceGroupDeploymentWhatIf
---

# New-ResourceGroupDeploymentWhatIf

## SYNOPSIS

Preview the changes an ARM or Bicep template deployment would make to a resource group without applying them.

## SYNTAX

### \_\_AllParameterSets

```
New-ResourceGroupDeploymentWhatIf -ResourceGroupName <string> -Name <string> [-SubscriptionId <string>]
 [-TemplateUri <string>] [-TemplateFile <string>] [-TemplateSpecId <string>] [-QueryString <string>]
 [-ParametersUri <string>] [-ParametersFile <string>] [-Mode <string>] [-Parameter <string[]>]
 [-ValidationLevel <string>] [-ResultFormat <string>] [<CommonParameters>]
```

## ALIASES

This cmdlet has the following aliases,
{{Insert list of aliases}}

## DESCRIPTION

Runs a what-if operation against the specified resource group using an ARM/Bicep template supplied either inline (`-TemplateFile`), via URL (`-TemplateUri`), or by referencing a template spec (`-TemplateSpecId`). Template parameters can be supplied via file, URI, query string, or as `Name=Value` pairs through `-Parameter`.
Unlike `New-ResourceGroupDeployment`, this cmdlet is read-only: it never creates, modifies, or deletes resources. Use it to validate and review the expected changes before committing to a real deployment.

## EXAMPLES

### Preview changes for a Bicep file against an existing resource group

Run a what-if analysis of `main.bicep` with parameter file `main.parameters.json` against resource group `myrg`.

```powershell
New-ResourceGroupDeploymentWhatIf -ResourceGroupName myrg -Name infra-prod-2026-06-18 -TemplateFile ./main.bicep -ParametersFile ./main.parameters.json
```

## PARAMETERS

### -Mode

Deployment mode used for the what-if analysis. Use `Incremental` (default) to preview adds and updates, or `Complete` to also preview which existing resources would be deleted.

```yaml
Type: System.String
DefaultValue: ""
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
HelpMessage: ""
```

### -Name

The name of the deployment used to track the what-if operation in deployment history.

```yaml
Type: System.String
DefaultValue: ""
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
HelpMessage: ""
```

### -Parameter

Template parameters supplied as inline `Name=Value` pairs. Use this when you don't have a parameters file. Repeat the flag or pass an array for multiple values.

```yaml
Type: System.String[]
DefaultValue: ""
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
HelpMessage: ""
```

### -ParametersFile

Path to a local ARM/Bicep parameters file (`*.parameters.json`).

```yaml
Type: System.String
DefaultValue: ""
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
HelpMessage: ""
```

### -ParametersUri

URI of a remote ARM/Bicep parameters file (for example, a blob URL).

```yaml
Type: System.String
DefaultValue: ""
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
HelpMessage: ""
```

### -QueryString

Optional SAS or query string appended to `-TemplateUri` / `-ParametersUri` when those URIs require authentication.

```yaml
Type: System.String
DefaultValue: ""
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
HelpMessage: ""
```

### -ResourceGroupName

The name of the resource group to run the what-if analysis against. The resource group must already exist.

```yaml
Type: System.String
DefaultValue: ""
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
HelpMessage: ""
```

### -ResultFormat

Controls how the what-if results are formatted. Common values are `FullResourcePayloads` (default, shows the full before/after resource state) and `ResourceIdOnly` (shows only the resource IDs that would change).

```yaml
Type: System.String
DefaultValue: ""
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
HelpMessage: ""
```

### -SubscriptionId

The Azure subscription ID that owns the target resource group. Defaults to the current account context when omitted.

```yaml
Type: System.String
DefaultValue: ""
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
HelpMessage: ""
```

### -TemplateFile

Path to a local ARM JSON or Bicep template file.

```yaml
Type: System.String
DefaultValue: ""
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
HelpMessage: ""
```

### -TemplateSpecId

Resource ID of an existing template spec to analyse (for example, `/subscriptions/.../providers/Microsoft.Resources/templateSpecs/<spec>/versions/<version>`).

```yaml
Type: System.String
DefaultValue: ""
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
HelpMessage: ""
```

### -TemplateUri

URI of a remote ARM/Bicep template (for example, a blob URL).

```yaml
Type: System.String
DefaultValue: ""
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
HelpMessage: ""
```

### -ValidationLevel

Deployment validation strictness. Common values are `Provider` (default) and `Template`. Use a stricter level to catch schema or parameter issues earlier.

```yaml
Type: System.String
DefaultValue: ""
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
HelpMessage: ""
```

### CommonParameters

This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable,
-InformationAction, -InformationVariable, -OutBuffer, -OutVariable, -PipelineVariable,
-ProgressAction, -Verbose, -WarningAction, and -WarningVariable. For more information, see
[about_CommonParameters](https://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

## OUTPUTS

### System.Object

Returns the what-if result object describing the predicted changes (resource additions, modifications, deletions, and no-changes) without applying them.

## NOTES

This cmdlet is read-only and never modifies resources. It intentionally omits `-WhatIf` and `-Confirm` parameters because there is no side effect to gate on.
The target resource group must already exist. Use `New-ResourceGroup` to create it first when bootstrapping a new environment.

## RELATED LINKS

- [New-ResourceGroupDeployment]()
- [New-ResourceGroup]()
- [Get-ResourceGroup]()
