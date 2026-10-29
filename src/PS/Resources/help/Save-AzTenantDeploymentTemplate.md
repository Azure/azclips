---
document type: cmdlet
external help file: Microsoft.Azure.Azclips.Cmdlets.Resources.dll-Help.xml
HelpUri: ''
Locale: en-US
Module Name: Resources
ms.date: 10/29/2026
PlatyPS schema version: 2024-05-01
title: Save-AzTenantDeploymentTemplate
---

# Save-AzTenantDeploymentTemplate

## SYNOPSIS

Save the template used for a tenant-scope deployment to a file.

## SYNTAX

### SaveByDeploymentName (Default)

```
Save-AzTenantDeploymentTemplate [-Name] <string> [-Path <string>] [-Force] [-Profile <string>]
 [-WhatIf] [-Confirm] [<CommonParameters>]
```

### SaveByDeploymentObject

```
Save-AzTenantDeploymentTemplate -DeploymentObject <PSDeployment> [-Path <string>] [-Force]
 [-Profile <string>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

## ALIASES

## DESCRIPTION

Exports the Azure Resource Manager template that a tenant-scope deployment was created from and writes it to a file on disk. The template is returned as submitted, with parameters and variables left unresolved. The cmdlet emits an object carrying the full path of the file that was written. Exporting a deployment that does not exist fails with `DeploymentNotFound`.

`-Path` follows the legacy rules: an existing directory receives `<deployment name>.json`; any other value is treated as the file itself and gains a `.json` extension when it lacks one. A relative path is resolved against the current PowerShell location, and missing parent directories are created. When `-Path` is omitted the file is written to the current location as `<deployment name>.json`.

If the destination file already exists the cmdlet fails rather than overwriting it. Pass `-Force` to overwrite.

## EXAMPLES

### Save a deployment template to the current directory

Writes `tenant-deploy.json` to the current location.

```powershell
Save-AzTenantDeploymentTemplate -Name tenant-deploy
```

### Save the template of a piped deployment to a directory, overwriting an existing file

```powershell
Get-AzTenantDeployment -Name tenant-deploy | Save-AzTenantDeploymentTemplate -Path C:\templates -Force
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

### -DeploymentObject

The deployment whose template to save, for example the output of `Get-AzTenantDeployment`.
The deployment object.

```yaml
Type: Microsoft.Azure.Azclips.Extensions.Resources.Models.PSDeployment
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: SaveByDeploymentObject
  Position: Named
  IsRequired: true
  ValueFromPipeline: true
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -Force

Overwrite the destination file when it already exists. Without this switch an existing file causes a terminating error and the file is left untouched.
Overwrite the destination file when it already exists.

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

### -Name

The name of the deployment whose template to save.
The name of the deployment.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases:
- DeploymentName
ParameterSets:
- Name: SaveByDeploymentName
  Position: 0
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -Path

The output path of the template file. An existing directory receives `<deployment name>.json`; any other value is used as the file name and gains a `.json` extension when it lacks one. Relative paths are resolved against the current PowerShell location. Defaults to the current location.
The output path of the template file.

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

### Microsoft.Azure.Azclips.Extensions.Resources.Models.PSDeployment

You can pipe a deployment from `Get-AzTenantDeployment`; its **DeploymentName** is used.

## OUTPUTS

### Microsoft.Azure.Azclips.Extensions.Resources.Processors.DeploymentTemplateSaveResult

An object with a single `Path` property carrying the full path of the template file that was written.

## NOTES

The exported template is the template as submitted; parameter references and variables are not replaced with their runtime values.

## RELATED LINKS


- [Get-AzTenantDeployment]()
- [New-AzTenantDeployment]()
