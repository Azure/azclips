---
document type: cmdlet
external help file: Microsoft.Azure.Azclips.Cmdlets.Resources.dll-Help.xml
HelpUri: ''
Locale: en-US
Module Name: Resources
ms.date: 10/15/2026
PlatyPS schema version: 2024-05-01
title: New-AzTenantDeployment
---

# New-AzTenantDeployment

## SYNOPSIS

Deploy an ARM or Bicep template at tenant scope.

## SYNTAX

### __AllParameterSets

```
New-AzTenantDeployment -Location <string> [-Name <string>] [-TemplateObject <hashtable>]
 [-ParametersUri <string>] [-ParametersFile <string>] [-Tag <hashtable>]
 [-DeploymentDebugLogLevel <string>] [-WhatIfResultFormat <string>]
 [-WhatIfExcludeChangeType <string[]>] [-ProceedIfNoChange] [-AsJob] [-TemplateUri <string>]
 [-TemplateFile <string>] [-TemplateSpecId <string>] [-QueryString <string>] [-Parameter <string[]>]
 [-ValidationLevel <string>] [-Profile <string>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

## ALIASES

## DESCRIPTION

Validates and then submits a deployment at tenant scope using an ARM/Bicep template supplied inline (`-TemplateFile` or `-TemplateObject`), via URL (`-TemplateUri`), or by referencing a template spec (`-TemplateSpecId`). Supply exactly one template source, or use a `.bicepparam` file with `-ParametersFile` that references its template. Template parameters can be supplied with `-ParametersFile`, `-ParametersUri`, or `-Parameter`. Required template parameters without a default value must be supplied; the cmdlet reports missing ones instead of prompting.
When `-Name` is omitted, the deployment is named after the template file, else the template URI file name, else a new GUID.
The cmdlet waits for the deployment to finish and returns the final deployment. If the deployment fails, the error lists up to three failed operation errors and the correlation ID.
Like the legacy cmdlet, tenant deployments always use `Incremental` mode.
`-WhatIf` runs a what-if operation and shows its change report instead of deploying. `-Confirm` shows the same report and asks before deploying.
`-AsJob` runs the deployment in the background and returns a job. Any what-if report and confirmation prompt are handled before the job starts.

## EXAMPLES

### Deploy a Bicep file at tenant scope

Submit `main.bicep` with parameter file `main.parameters.json` as deployment `tenant-infra` at tenant scope.

```powershell
New-AzTenantDeployment -Location eastus -Name tenant-infra -TemplateFile ./main.bicep -ParametersFile ./main.parameters.json
```

### Deploy a remote template with inline parameters

```powershell
New-AzTenantDeployment -Location eastus -Name tenant-infra -TemplateUri https://example.com/azuredeploy.json -Parameter policyName=audit-tags
```

### Deploy without a name

The deployment is named `main` after the template file.

```powershell
New-AzTenantDeployment -Location eastus -TemplateFile ./main.json
```

### Preview the changes without deploying

```powershell
New-AzTenantDeployment -Location eastus -Name tenant-infra -TemplateFile ./main.bicep -WhatIf
```

### Deploy in the background

Start the deployment as a background job. Use the standard PowerShell job cmdlets to wait for the job and receive the deployment.

```powershell
New-AzTenantDeployment -Location eastus -Name tenant-infra -TemplateFile ./main.bicep -AsJob
```

## PARAMETERS

### -AsJob

Run cmdlet in the background

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

The deployment debug log level: `All`, `RequestContent`, `ResponseContent`, or `None`. ARM logs the selected request and/or response content of the deployment. The cmdlet warns that this content, which may include secrets, becomes readable through the deployment operations.

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

The name of the deployment. Used to track and reference the deployment in deployment history. Defaults to the template file name, else the template URI file name, both without extension, else a new GUID.

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

### -ProceedIfNoChange

With `-Confirm`, deploy without asking when the what-if reports only `NoChange` or `Ignore` changes. The what-if report is still written to the host.

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

### -Tag

Tags to put on the deployment, as a hash table of name/value pairs, for example `@{ env = 'prod' }`.

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

### -TemplateSpecId

Resource ID of an existing template spec version to deploy (for example, `/subscriptions/.../providers/Microsoft.Resources/templateSpecs/<spec>/versions/<version>`).

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

### -WhatIfExcludeChangeType

Change types to leave out of the what-if report shown by `-WhatIf` or `-Confirm`, for example `NoChange`, `Ignore`.

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

### -WhatIfResultFormat

The what-if result format used by `-WhatIf` or `-Confirm`: `FullResourcePayloads` (default) or `ResourceIdOnly`.

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

### Microsoft.Azure.Azclips.Extensions.Resources.Models.PSDeployment

The completed `PSDeployment` object containing the deployment name, provisioning state, timestamp, parameters, outputs declared by the template, and correlation ID. `ParametersString` and `OutputsString` show the parameters and outputs as tables.

### System.Management.Automation.Job

With `-AsJob`, returns the background job. Receive the job to get the `PSDeployment` object.
## NOTES

The deployment is validated first; a validation failure stops the cmdlet before anything is deployed.

Differences from Az.Resources: Template parameters are passed with `-Parameter`, `-ParametersFile`, or `-ParametersUri` instead of dynamic parameters or `-TemplateParameterObject`. The legacy parameter names `-TemplateParameterFile` and `-TemplateParameterUri` are accepted as aliases. `-SkipTemplateParameterPrompt` and `-Pre` are not supported.

## RELATED LINKS

- [Test-AzTenantDeployment]()
- [Get-AzTenantDeployment]()
- [Remove-AzTenantDeployment]()
