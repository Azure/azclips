---
document type: cmdlet
external help file: Microsoft.Azure.Azclips.Cmdlets.Resources.dll-Help.xml
HelpUri: ''
Locale: en-US
Module Name: Resources
ms.date: 10-30-2026
PlatyPS schema version: 2024-05-01
title: Save-AzDeploymentTemplate
---

# Save-AzDeploymentTemplate

## SYNOPSIS

Save the template used for a subscription deployment to a file.

## SYNTAX

### __AllParameterSets

```
Save-AzDeploymentTemplate -Name <string> [-SubscriptionId <string>] [-Path <string>] [-Force]
 [-Profile <string>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

## ALIASES

This cmdlet has the following aliases,
  Save-AzSubscriptionDeploymentTemplate

## DESCRIPTION

Exports the Azure Resource Manager template that a subscription-scoped deployment was created from and writes it to a file on disk. The operation is read-only: it never creates or modifies resources. Azure Resource Manager returns the template exactly as it was submitted, with parameters and variables left unresolved, so the saved file can be fed straight back into `New-AzDeployment -TemplateFile`.

The cmdlet emits an object carrying the full path of the file that was written. Exporting a deployment that does not exist fails with `DeploymentNotFound`. The subscription defaults to the active profile's subscription unless `-SubscriptionId` is supplied.

`-Path` follows the same rules as `Save-AzResourceGroupDeploymentTemplate`: when it names an existing directory the file is written there as `<deployment name>.json`; any other value is treated as the file itself and gains a `.json` extension when it lacks one. A relative path is resolved against the current PowerShell location, and any missing parent directories are created. When `-Path` is omitted the file is written to the current location as `<deployment name>.json`.

If the destination file already exists the cmdlet fails rather than overwriting it. Pass `-Force` to overwrite.

## EXAMPLES

### Save a deployment template to the current directory

Writes `sub-baseline-2026-10-30.json` to the current location.

```powershell
Save-AzDeploymentTemplate -Name sub-baseline-2026-10-30
```

### Save a deployment template to a specific directory

The directory already exists, so the file is written as `C:\templates\sub-baseline-2026-10-30.json`.

```powershell
Save-AzDeploymentTemplate -Name sub-baseline-2026-10-30 -Path C:\templates
```

### Save a deployment template under a chosen file name

```powershell
Save-AzDeploymentTemplate -Name sub-baseline-2026-10-30 -Path .\exported\sub-baseline.json
```

### Overwrite an existing file

Without `-Force` a second run against the same destination fails instead of replacing the file.

```powershell
Save-AzDeploymentTemplate -Name sub-baseline-2026-10-30 -Path .\sub-baseline.json -Force
```

### Redeploy from the saved template

```powershell
$saved = Save-AzDeploymentTemplate -Name sub-baseline-2026-10-30 -Force
New-AzDeployment -Name sub-baseline-replay -Location eastus -TemplateFile $saved.Path
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

### -Force

Overwrite the destination file when it already exists. Without this switch an existing file causes a terminating error and the file is left untouched.

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

### -Name

The name of the deployment whose template to save.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases:
- DeploymentName
ParameterSets:
- Name: (All)
  Position: Named
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: true
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -Path

The output path of the template file. An existing directory receives `<deployment name>.json`; any other value is used as the file name and gains a `.json` extension when it lacks one. Relative paths are resolved against the current PowerShell location. Defaults to the current location.

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
  ValueFromPipelineByPropertyName: true
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -Profile

Use the named profile for this command, overriding the active profile.

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

Azure subscription id. Defaults to the active profile's subscription.

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

### System.String

You can pipe the **Name** and **Path** values to this cmdlet by property name, for example the output of `Get-AzDeployment`.

## OUTPUTS

### Microsoft.Azure.Azclips.Extensions.Resources.Processors.DeploymentTemplateSaveResult

An object with a single `Path` property carrying the full path of the template file that was written.

## NOTES

The exported template is the template as submitted, not the resolved deployment output: parameter references and variables are preserved rather than replaced with their runtime values. Use `Get-AzDeployment` to inspect the parameter values a deployment actually ran with.

## RELATED LINKS


- [Get-AzDeployment]()
- [New-AzDeployment]()
- [Save-AzResourceGroupDeploymentTemplate]()
