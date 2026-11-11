---
document type: cmdlet
external help file: Microsoft.Azure.Azclips.Cmdlets.Resources.dll-Help.xml
HelpUri: ''
Locale: en-US
Module Name: Resources
ms.date: 11/05/2026
PlatyPS schema version: 2024-05-01
title: Get-AzTenantDeploymentWhatIfResult
---

# Get-AzTenantDeploymentWhatIfResult

## SYNOPSIS

Preview the changes a tenant deployment would make, without deploying it.

## SYNTAX

### __AllParameterSets

```
Get-AzTenantDeploymentWhatIfResult -Location <string> [-Name <string>] [-ParametersUri <string>]
 [-ParametersFile <string>] [-ResultFormat <string>] [-ExcludeChangeType <string[]>]
 [-TemplateUri <string>] [-TemplateFile <string>] [-TemplateSpecId <string>] [-QueryString <string>]
 [-Parameter <string[]>] [-ValidationLevel <string>] [-Profile <string>] [<CommonParameters>]
```

## ALIASES

## DESCRIPTION

Runs an Azure Resource Manager what-if operation for an ARM/Bicep template at tenant scope and returns a structured `WhatIfOperationResult` with `Status` and `Changes` properties, without creating or modifying any resources. The template is supplied inline (`-TemplateFile`), via URL (`-TemplateUri`), or by referencing a template spec (`-TemplateSpecId`). Supply exactly one template source, or use a `.bicepparam` file with `-ParametersFile` that references its template. Template parameters can be supplied with `-ParametersFile`, `-ParametersUri`, or `-Parameter`.

When the what-if operation fails, the cmdlet writes the error and terminates.
Like the legacy cmdlet, what-if always uses `Incremental` mode.

## EXAMPLES

### Preview a Bicep template before deploying it

```powershell
Get-AzTenantDeploymentWhatIfResult -Location eastus -TemplateFile ./main.bicep -ParametersFile ./main.parameters.json
```

### Preview only the resource IDs, leaving out unchanged resources

```powershell
Get-AzTenantDeploymentWhatIfResult -Location eastus -TemplateUri https://example.com/azuredeploy.json -ResultFormat ResourceIdOnly -ExcludeChangeType NoChange
```

## PARAMETERS

### -ExcludeChangeType

Resource change types to leave out of the what-if result. Allowed values: `Create`, `Delete`, `Deploy`, `Ignore`, `Modify`, `NoChange`, `Unsupported`.

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

Template parameters. Each value can be a `Name=Value` pair, a JSON object, the path to a JSON parameters file (optionally prefixed with `@`), or the path to a `.bicepparam` file. Repeat the flag or pass an array for multiple values. Values override matching keys from `-ParametersFile`. A `Name=Value` pair for a parameter the local template declares is read as that parameter's type (`string`, `int`, `bool`, `object`, `array`); other values are parsed as JSON when possible, else as strings. A `.bicepparam` path is used as the parameters file, and with a `.bicepparam` file the `Name=Value` values are compiled into it as overrides (Bicep CLI 0.22.6 or later). Cannot be combined with `-ParametersUri`.

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

URI of a remote JSON parameters file (for example, a blob URL). Include any required SAS token in the URI itself. Cannot be combined with `-ParametersFile` or `-Parameter`.

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

The format of the what-if results. `FullResourcePayloads` (default) or `ResourceIdOnly`.

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
AcceptedValues:
- FullResourcePayloads
- ResourceIdOnly
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

The structured what-if result, including its status and predicted resource changes in `Changes`. No resources are created or modified.

## NOTES

What-if is read-only: it never creates or modifies resources. `New-AzTenantDeployment -WhatIf` returns the same result type.

Differences from Az.Resources: this cmdlet returns the `WhatIfOperationResult` model instead of a `PSWhatIfOperationResult` object. `-TemplateParameterObject`, `-TemplateObject`, dynamic template parameters, `-SkipTemplateParameterPrompt`, and `-Pre` are not supported.

## RELATED LINKS


- [New-AzTenantDeployment]()
- [Test-AzTenantDeployment]()