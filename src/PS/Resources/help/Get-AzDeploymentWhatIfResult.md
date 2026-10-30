---
document type: cmdlet
external help file: Microsoft.Azure.Azclips.Cmdlets.Resources.dll-Help.xml
HelpUri: ''
Locale: en-US
Module Name: Resources
ms.date: 10-30-2026
PlatyPS schema version: 2024-05-01
title: Get-AzDeploymentWhatIfResult
---

# Get-AzDeploymentWhatIfResult

## SYNOPSIS

Preview the changes an ARM or Bicep deployment would make at the subscription scope without applying them.

## SYNTAX

### __AllParameterSets

```
Get-AzDeploymentWhatIfResult -Location <string> [-Name <string>] [-TemplateUri <string>]
 [-TemplateFile <string>] [-TemplateSpecId <string>] [-TemplateObject <hashtable>]
 [-TemplateParameterObject <hashtable>] [-QueryString <string>] [-ParametersUri <string>] [-ParametersFile <string>]
 [-Parameter <string[]>] [-ValidationLevel <string>] [-ResultFormat <string>]
 [-ExcludeChangeType <string[]>] [-SubscriptionId <string>] [-Profile <string>]
 [<CommonParameters>]
```

## ALIASES

This cmdlet has the following aliases,
  Get-AzSubscriptionDeploymentWhatIfResult

## DESCRIPTION

Runs an ARM/Bicep what-if operation at the target subscription and returns the changes the deployment would make — creations, modifications, deletions, and unchanged or ignored resources — without creating or modifying any resources. It sends the same request as `New-AzDeployment` would validate, so use it to review a subscription deployment before running `New-AzDeployment`. The template is supplied as a local file (`-TemplateFile`), a URL (`-TemplateUri`), a template spec (`-TemplateSpecId`), or an inline hashtable (`-TemplateObject`). Template parameters can be supplied via file, URI, query string, an inline hashtable (`-TemplateParameterObject`), or as `Name=Value` pairs through `-Parameter`.
Use `-ResultFormat ResourceIdOnly` for a compact list of affected resources, and `-ExcludeChangeType` to drop change types such as `NoChange` from the result.

## EXAMPLES

### Preview a Bicep deployment at subscription scope

Run a what-if for `main.bicep` with parameter file `main.parameters.json` as deployment `sub-infra-2026-06-16`, storing the what-if metadata in `eastus`.

```powershell
Get-AzDeploymentWhatIfResult -Name sub-infra-2026-06-16 -Location eastus -TemplateFile ./main.bicep -ParametersFile ./main.parameters.json
```

### List only the IDs of the affected resources

Run a what-if for a template that creates a resource group and return only the resource IDs, without the predicted resource bodies.

```powershell
Get-AzDeploymentWhatIfResult -Location westus2 -TemplateFile ./create-rg.json -Parameter rgName=demo-rg -ResultFormat ResourceIdOnly
```

### Hide unchanged and ignored resources

Run a what-if for `main.bicep` and remove the `NoChange` and `Ignore` entries from the result, so only real changes remain.

```powershell
Get-AzDeploymentWhatIfResult -Location eastus -TemplateFile ./main.bicep -ExcludeChangeType NoChange, Ignore
```

## PARAMETERS

### -ExcludeChangeType

Change types to remove from the what-if result, matched case-insensitively. Changes of these types are dropped from the result's change list before it is written; the what-if request is unchanged, and potential changes and diagnostics are not filtered. An unknown change type throws an error listing the allowed values before any request is sent.

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
AcceptedValues:
- Create
- Delete
- Deploy
- Ignore
- Modify
- NoChange
- Unsupported
HelpMessage: ''
```

### -Location

The Azure region that stores the deployment what-if operation metadata, for example `eastus`. Subscription-scope deployments have no resource group to inherit a region from, so a location is required.

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

The name of the deployment to evaluate. When omitted, defaults to the template file or URI name without its extension, or a generated GUID when no valid name can be derived.

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
Values are typed by the parameter's declared type when the template is local or is
downloaded from `-TemplateUri`: `string` values are kept as-is, `int` and `bool` values
must parse, and `object` and `array` values must be valid JSON, otherwise the cmdlet throws
naming the parameter. Otherwise, values are parsed as JSON when possible; otherwise they
are strings.
A value that is an `http://` or `https://` URL of a JSON parameters file is downloaded
without Azure credentials, and its `parameters` are merged like a local parameters file;
a SAS URL is read as a URL, not as a `Name=Value` pair. A failed download throws.
A value that is a path ending in `.bicepparam` is used as the parameters file, exactly
as if given as `-ParametersFile`, and the other values overlay it; combining it with
`-ParametersFile` or another `.bicepparam` path throws an error.

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

Path to a local ARM/Bicep parameters file (`*.parameters.json` or `*.bicepparam`). A `.bicepparam` file names its own template through its `using` declaration, so no template parameter is needed alongside it. A `.bicepparam` file requires Bicep CLI 0.16.1 or later, or 0.22.6 or later when combined with inline `-Parameter` values; with an older Bicep CLI the cmdlet throws an error asking to upgrade and sends no request.

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

### -ResultFormat

The format of the what-if results. Use `FullResourcePayloads` (default) to include the predicted resource bodies and property deltas, or `ResourceIdOnly` for a compact list of affected resource IDs.

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

### -SubscriptionId

The Azure subscription to run the what-if against. Defaults to the active profile's subscription when omitted.

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

### -TemplateParameterObject

Template parameter values supplied as a hashtable, for example `@{ storagePrefix = 'st'; tags = @{ env = 'test' } }`. Each entry is sent as the parameter's value. An entry whose value is a hashtable containing a `reference` key, such as a Key Vault reference `@{ reference = @{ keyVault = @{ id = '/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/myrg/providers/Microsoft.KeyVault/vaults/mykeyvault' }; secretName = 'adminPassword' } }`, is sent as written. Cannot be combined with `-ParametersFile` or `-ParametersUri`; `-Parameter` values override same-named entries.

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

Resource ID of an existing template spec version to evaluate (for example, `/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Resources/templateSpecs/{name}/versions/{version}`).

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
The deployment request links to this URI. Unless `-ParametersUri` is given, the template is
also downloaded without Azure credentials (with `-QueryString` applied) to type `-Parameter`
values and to report missing required parameters before the request is sent; a failed
download throws.

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

What-if validation strictness. Common values are `Provider` (default) and `Template`. Use a stricter level to catch schema or parameter issues earlier.

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

A `WhatIfOperationResult` with the changes the deployment would make, the potential changes what-if could not fully evaluate, and diagnostics. No resources are created or modified.

## NOTES

What-if is a read-only preview: it never creates or modifies resources. Use `New-AzDeployment` to apply the changes once the preview looks correct. Subscription-scope templates must use the `subscriptionDeploymentTemplate.json` schema, or `targetScope = 'subscription'` in Bicep.

The legacy Az.Resources name `Get-AzSubscriptionDeploymentWhatIfResult` and the parameter names `-DeploymentName`, `-TemplateParameterFile`, and `-TemplateParameterUri` are accepted as aliases.

## RELATED LINKS

- [New-AzDeployment]()
- [Test-AzDeployment]()
- [New-AzResourceGroupDeploymentWhatIf]()
