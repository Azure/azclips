---
document type: cmdlet
external help file: Microsoft.Azure.Azclips.Cmdlets.Resources.dll-Help.xml
HelpUri: ''
Locale: en-US
Module Name: Resources
ms.date: 10-09-2026
PlatyPS schema version: 2024-05-01
title: Test-AzDeployment
---

# Test-AzDeployment

## SYNOPSIS

Validate whether an ARM or Bicep template would be accepted for a subscription-scope deployment, without deploying it.

## SYNTAX

### __AllParameterSets

```
Test-AzDeployment -Location <string> [-Name <string>] [-TemplateUri <string>]
 [-TemplateFile <string>] [-TemplateSpecId <string>] [-TemplateObject <hashtable>]
 [-QueryString <string>] [-ParametersUri <string>] [-ParametersFile <string>]
 [-Parameter <string[]>] [-ValidationLevel <string>] [-SubscriptionId <string>] [-Profile <string>]
 [<CommonParameters>]
```

## ALIASES

This cmdlet has the following aliases,
  Test-AzSubscriptionDeployment

## DESCRIPTION

Asks Azure Resource Manager whether a template is syntactically correct and would be accepted for deployment at the subscription scope, without creating or modifying any resources. The template is supplied as a local file (`-TemplateFile`), a URL (`-TemplateUri`), a template spec (`-TemplateSpecId`), or an inline hashtable (`-TemplateObject`). Template parameters can be supplied via file, URI, query string, or as `Name=Value` pairs through `-Parameter`.

When the template is valid the validation result is returned; when it is not, the cmdlet writes the validation errors and terminates with a non-zero exit code.

## EXAMPLES

### Validate a Bicep template before deploying it

Validate `main.bicep` with parameter file `main.parameters.json` as deployment `sub-infra-2026-06-18`.

```powershell
Test-AzDeployment -Name sub-infra-2026-06-18 -Location eastus -TemplateFile ./main.bicep -ParametersFile ./main.parameters.json
```

### Validate a remote template with inline parameters

```powershell
Test-AzDeployment -Location westus2 -TemplateUri https://contoso.blob.core.windows.net/templates/create-rg.json -Parameter rgName=demo-rg, rgLocation=westus2
```

## PARAMETERS

### -Location

The Azure region that stores the deployment metadata, for example `eastus`. Subscription-scope deployments have no resource group to inherit a region from, so a location is required.

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

The deployment name to validate. When omitted, defaults to the template file or URI name without its extension, or a generated GUID when no valid name can be derived.

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

Template parameters supplied as inline `Name=Value` pairs. Use this when you don't have a parameters file. Pass an array for multiple values.
Values are typed by the parameter's declared type when the template is local: `string`
values are kept as-is, `int` and `bool` values must parse, and `object` and `array` values
must be valid JSON, otherwise the cmdlet throws naming the parameter. Without a local
template, values are parsed as JSON when possible; otherwise they are strings.

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

Path to a local ARM/Bicep parameters file (`*.parameters.json` or `*.bicepparam`). A `.bicepparam` file names its own template through its `using` declaration, so no template parameter is needed alongside it.

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

URI of a remote ARM parameters file (for example, a blob URL).

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

Optional SAS or query string appended to `-TemplateUri` / `-ParametersUri` when those URIs require authentication.

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

The Azure subscription that owns the deployment. Defaults to the active profile's subscription when omitted.

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

Path to a local ARM JSON or Bicep template file.

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

An inline ARM template supplied as a hashtable, for example the result of `Get-Content ./azuredeploy.json -Raw | ConvertFrom-Json -AsHashtable`.

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

Resource ID of an existing template spec version to deploy (for example, `/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Resources/templateSpecs/{name}/versions/{version}`).

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

Deployment validation strictness. Common values are `Provider` (default) and `Template`. Use a stricter level to catch schema or parameter issues earlier.

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

### Microsoft.Azure.Azclips.Extensions.Resources.Models.DeploymentValidateResult

A `DeploymentValidateResult` describing the outcome of the validation, including the deployment properties Azure Resource Manager resolved. No resources are created or modified.

## NOTES

Validation is read-only: it never creates or modifies resources. A non-zero exit code means the template would be rejected. Use `New-AzDeployment` to deploy once validation passes.

Differences from Az.Resources: on success this cmdlet returns the `DeploymentValidateResult` instead of no output, and validation failures are written to the error stream instead of the output stream. The legacy name `Test-AzSubscriptionDeployment` and the parameter names `-DeploymentName`, `-TemplateParameterFile`, and `-TemplateParameterUri` are accepted as aliases.

## RELATED LINKS

- [New-AzDeployment]()
- [Get-AzDeployment]()
- [Test-AzResourceGroupDeployment]()
