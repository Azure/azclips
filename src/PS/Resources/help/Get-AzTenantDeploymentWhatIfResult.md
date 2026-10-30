---
document type: cmdlet
external help file: Microsoft.Azure.Azclips.Cmdlets.Resources.dll-Help.xml
HelpUri: ''
Locale: en-US
Module Name: Resources
ms.date: 10/30/2026
PlatyPS schema version: 2024-05-01
title: Get-AzTenantDeploymentWhatIfResult
---

# Get-AzTenantDeploymentWhatIfResult

## SYNOPSIS

Get the what-if result of a tenant deployment, without deploying it.

## SYNTAX

### __AllParameterSets

```
Get-AzTenantDeploymentWhatIfResult -Location <string> [-Name <string>] [-TemplateObject <hashtable>]
 [-ParametersUri <string>] [-ParametersFile <string>] [-ResultFormat <string>]
 [-ExcludeChangeType <string[]>] [-TemplateUri <string>] [-TemplateFile <string>]
 [-TemplateSpecId <string>] [-QueryString <string>] [-Parameter <string[]>]
 [-TemplateParameterObject <hashtable>] [-ValidationLevel <string>] [-SkipTemplateParameterPrompt]
 [-Profile <string>] [<CommonParameters>]
```

## ALIASES

## DESCRIPTION

Runs a what-if operation for a tenant-scope deployment and returns the result: the resource changes the deployment would make. No resources are created or modified. Change types listed in `-ExcludeChangeType` are removed from the result. A template error fails the cmdlet.
With a local template (`-TemplateFile` or `-TemplateObject`), or with only a `.bicepparam` `-ParametersFile` whose `using` declaration names a local template, each template parameter is also a cmdlet parameter, for example `-siteName`; a template parameter that has the same name as a cmdlet parameter gets the suffix `FromTemplate`, for example `-LocationFromTemplate`. These values override values from the other parameter sources. PowerShell prompts for a required template parameter that has no default value and no value from another source. With `-SkipTemplateParameterPrompt`, the cmdlet reports missing values instead of prompting.

## EXAMPLES

### Preview a Bicep template

```powershell
Get-AzTenantDeploymentWhatIfResult -Location westus -TemplateFile .\main.bicep
```

### Show only the resource IDs of created and modified resources

```powershell
Get-AzTenantDeploymentWhatIfResult -Location westus -TemplateFile .\main.json -ResultFormat ResourceIdOnly -ExcludeChangeType NoChange, Ignore
```

## PARAMETERS

### -ExcludeChangeType

Resource change types to leave out of the result. Allowed values: Create, Delete, Deploy, Ignore, Modify, NoChange, Unsupported.

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

### -Location

The Azure location where the deployment metadata is stored.

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

### -Name

The name of the deployment. Defaults to the template file name, else the template URI file name, both without extension, else a new GUID.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases:
- DeploymentName
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

### -Parameter

Template parameters. Each value can be a `Name=Value` pair, a JSON object, the path to a JSON parameters file (optionally prefixed with `@`), the path to a `.bicepparam` file, or the URL of a JSON parameters file. Repeat the flag or pass an array for multiple values. Values override matching keys from `-ParametersFile`. A `Name=Value` pair for a parameter the local template declares is read as that parameter's type (`string`, `int`, `bool`, `object`, `array`); other values are parsed as JSON when possible, else as strings. A `.bicepparam` path is used as the parameters file, and with a `.bicepparam` file the `Name=Value` values are compiled into it as overrides (Bicep CLI 0.22.6 or later). A URL must be the only `-Parameter` value; it is sent as a parameters link, like `-ParametersUri`. Cannot be combined with `-ParametersUri`.

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

Path to a local parameters file (`*.json` or `*.bicepparam`). A `.bicepparam` file can also supply the template through its `using` reference. Cannot be combined with `-ParametersUri`.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases:
- TemplateParameterFile
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

URI of a remote JSON parameters file (for example, a blob URL). Include any required SAS token in the URI itself. Cannot be combined with `-ParametersFile` or `-Parameter`.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases:
- TemplateParameterUri
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

### -QueryString

Optional SAS token or other query string for `-TemplateUri`. Requires `-TemplateUri` and applies only to the template link, not to `-ParametersUri`.

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

### -ResultFormat

The format of the what-if result: `FullResourcePayloads` (default) or `ResourceIdOnly`.

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

### -SkipTemplateParameterPrompt

Does not add template parameters as cmdlet parameters, so PowerShell never prompts for missing template parameter values. Missing required values are reported as an error instead, which suits non-interactive scripts.

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

### -TemplateFile

Path to a local ARM JSON or Bicep template file. Bicep files require the Bicep CLI.

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

### -TemplateObject

A hash table which represents the template.

```yaml
Type: System.Collections.Hashtable
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

### -TemplateParameterObject

A hash table of template parameter values, for example `@{ adminName = 'admin'; count = 2 }`. Each value is sent as `{ "value": ... }`, except a hash table with a `reference` key (a Key Vault reference), which is sent as is. These values override `-ParametersFile` and `-Parameter`; template parameters given as cmdlet parameters override them.

```yaml
Type: System.Collections.Hashtable
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

Resource ID of an existing template spec version to preview (for example, `/subscriptions/.../providers/Microsoft.Resources/templateSpecs/<spec>/versions/<version>`).

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

URI of a remote ARM JSON template (for example, a blob URL).

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

Validation strictness. `Template` checks the template only, `Provider` also calls resource providers, and `ProviderNoRbac` calls providers while skipping RBAC checks. When omitted, Azure Resource Manager uses its default level.

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

### Microsoft.Azure.Azclips.Extensions.Resources.Models.WhatIfOperationResult

The what-if result, including the predicted resource changes.

## NOTES

Differences from Az.Resources: the cmdlet returns `WhatIfOperationResult` instead of `PSWhatIfOperationResult`. The legacy parameter names `-TemplateParameterFile` and `-TemplateParameterUri` are accepted as aliases. Template parameters from a `-TemplateUri` or `-TemplateSpecId` template are not added as cmdlet parameters. `-Pre` is not supported.

## RELATED LINKS

- [New-AzTenantDeployment]()
- [Test-AzTenantDeployment]()
