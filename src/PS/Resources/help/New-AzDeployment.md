---
document type: cmdlet
external help file: Microsoft.Azure.Azclips.Cmdlets.Resources.dll-Help.xml
HelpUri: ''
Locale: en-US
Module Name: Resources
ms.date: 10-09-2026
PlatyPS schema version: 2024-05-01
title: New-AzDeployment
---

# New-AzDeployment

## SYNOPSIS

Deploy an ARM or Bicep template at the subscription scope.

## SYNTAX

### __AllParameterSets

```
New-AzDeployment -Location <string> [-Name <string>] [-TemplateUri <string>]
 [-TemplateFile <string>] [-TemplateSpecId <string>] [-TemplateObject <hashtable>]
 [-TemplateParameterObject <hashtable>] [-QueryString <string>] [-ParametersUri <string>] [-ParametersFile <string>]
 [-Parameter <string[]>] [-ValidationLevel <string>] [-Tag <hashtable>]
 [-DeploymentDebugLogLevel <string>] [-SubscriptionId <string>] [-Profile <string>] [-WhatIf]
 [-Confirm] [<CommonParameters>]
```

## ALIASES

This cmdlet has the following aliases,
  New-AzSubscriptionDeployment

## DESCRIPTION

Submits a deployment to the target subscription. Use subscription-scope deployments for resources that live outside a resource group, such as resource groups themselves, policy assignments, and role assignments. The template is supplied as a local file (`-TemplateFile`), a URL (`-TemplateUri`), a template spec (`-TemplateSpecId`), or an inline hashtable (`-TemplateObject`). Template parameters can be supplied via file, URI, query string, an inline hashtable (`-TemplateParameterObject`), or as `Name=Value` pairs through `-Parameter`. Deployments run in `Incremental` mode. The cmdlet first validates the deployment and, when validation fails, throws `Deployment validation failed.` with the ARM error without submitting the deployment.

## EXAMPLES

### Deploy a Bicep file at subscription scope

Submit `main.bicep` with parameter file `main.parameters.json` as deployment `sub-infra-2026-06-16`, storing the deployment metadata in `eastus`.

```powershell
New-AzDeployment -Name sub-infra-2026-06-16 -Location eastus -TemplateFile ./main.bicep -ParametersFile ./main.parameters.json
```

### Deploy a remote template with inline parameters

Deploy a template hosted at a URL that creates a resource group, overriding its parameters inline.

```powershell
New-AzDeployment -Location westus2 -TemplateUri https://contoso.blob.core.windows.net/templates/create-rg.json -Parameter rgName=demo-rg, rgLocation=westus2
```

### Deploy a template with deployment tags

Deploy `main.bicep` and tag the deployment with `env=test` and `owner=ops`.

```powershell
New-AzDeployment -Location eastus -TemplateFile ./main.bicep -Tag @{ env = 'test'; owner = 'ops' }
```

### Deploy a template with debug logging

Deploy `main.bicep` and log both the request and the response content, so they can be read from the deployment operations. A warning reminds you that the logged content may include secrets.

```powershell
New-AzDeployment -Location eastus -TemplateFile ./main.bicep -DeploymentDebugLogLevel All
```

### Deploy a template with parameters from a hashtable

Deploy `main.json`, passing its parameters as a hashtable instead of a parameters file. The `adminPassword` entry is a Key Vault reference and is sent as written.

```powershell
New-AzDeployment -Location eastus -TemplateFile ./main.json -TemplateParameterObject @{
    storagePrefix = 'st'
    adminPassword = @{ reference = @{ keyVault = @{ id = '/subscriptions/00000000-0000-0000-0000-000000000000/resourceGroups/myrg/providers/Microsoft.KeyVault/vaults/mykeyvault' }; secretName = 'adminPassword' } }
}
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

### -DeploymentDebugLogLevel

The debug log level of the deployment: `RequestContent`, `ResponseContent`, `All` (both) or `None` (case-insensitive). The level is sent as the deployment's `properties.debugSetting.detailLevel` in both the validate request and the deployment request, and a warning is written because the logged content is readable from the deployment operations and may include secrets such as passwords or keys. `None`, or omitting the parameter, logs nothing. Any other value is rejected as invalid input before a request is sent.

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

The name of the deployment. Used to track and reference the deployment in the subscription's deployment history. When omitted, defaults to the template file or URI name without its extension, or a generated GUID when no valid name can be derived.

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

### -Tag

Tags to put on the deployment, as a hashtable of name-value pairs, for example `@{ env = 'test' }`. They are sent as the deployment's `tags` in both the validate request and the deployment request. Values are converted to strings. When omitted or empty, no tags are sent.

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

## OUTPUTS

### Microsoft.Azure.Azclips.Extensions.Resources.Models.DeploymentExtended

The completed `DeploymentExtended` object containing the deployment name, provisioning state, outputs declared by the template, and correlation ID.

## NOTES

Subscription-scope templates must use the `subscriptionDeploymentTemplate.json` schema, or `targetScope = 'subscription'` in Bicep. Use `New-AzResourceGroupDeployment` to deploy into a resource group instead.

The legacy Az.Resources name `New-AzSubscriptionDeployment` and the parameter names `-DeploymentName`, `-TemplateParameterFile`, and `-TemplateParameterUri` are accepted as aliases.

## RELATED LINKS

- [Test-AzDeployment]()
- [Get-AzDeployment]()
- [New-AzResourceGroupDeployment]()
