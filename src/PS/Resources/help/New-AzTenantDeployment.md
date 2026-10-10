---
document type: cmdlet
external help file: Microsoft.Azure.Azclips.Cmdlets.Resources.dll-Help.xml
HelpUri: ''
Locale: en-US
Module Name: Resources
ms.date: 10/10/2026
PlatyPS schema version: 2024-05-01
title: New-AzTenantDeployment
---

# New-AzTenantDeployment

## SYNOPSIS

Create a deployment at tenant scope.

## SYNTAX

### ByTemplateFileWithNoParameters (Default)

```
New-AzTenantDeployment -Location <string> -TemplateFile <string> [-Name <string>]
 [-DeploymentDebugLogLevel <string>] [-Tag <hashtable>] [-WhatIfResultFormat <string>]
 [-WhatIfExcludeChangeType <string[]>] [-ProceedIfNoChange] [-AsJob] [-SkipTemplateParameterPrompt]
 [-QueryString <string>] [-ValidationLevel <string>] [-Pre] [-Profile <string>] [-WhatIf] [-Confirm]
 [<CommonParameters>]
```

### ByTemplateObjectAndParameterObject

```
New-AzTenantDeployment -Location <string> -TemplateParameterObject <hashtable>
 -TemplateObject <hashtable> [-Name <string>] [-DeploymentDebugLogLevel <string>] [-Tag <hashtable>]
 [-WhatIfResultFormat <string>] [-WhatIfExcludeChangeType <string[]>] [-ProceedIfNoChange] [-AsJob]
 [-SkipTemplateParameterPrompt] [-QueryString <string>] [-ValidationLevel <string>] [-Pre]
 [-Profile <string>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### ByTemplateFileAndParameterObject

```
New-AzTenantDeployment -Location <string> -TemplateParameterObject <hashtable>
 -TemplateFile <string> [-Name <string>] [-DeploymentDebugLogLevel <string>] [-Tag <hashtable>]
 [-WhatIfResultFormat <string>] [-WhatIfExcludeChangeType <string[]>] [-ProceedIfNoChange] [-AsJob]
 [-SkipTemplateParameterPrompt] [-QueryString <string>] [-ValidationLevel <string>] [-Pre]
 [-Profile <string>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### ByTemplateUriAndParameterObject

```
New-AzTenantDeployment -Location <string> -TemplateParameterObject <hashtable> -TemplateUri <string>
 [-Name <string>] [-DeploymentDebugLogLevel <string>] [-Tag <hashtable>]
 [-WhatIfResultFormat <string>] [-WhatIfExcludeChangeType <string[]>] [-ProceedIfNoChange] [-AsJob]
 [-SkipTemplateParameterPrompt] [-QueryString <string>] [-ValidationLevel <string>] [-Pre]
 [-Profile <string>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### ByTemplateSpecResourceIdAndParamsObject

```
New-AzTenantDeployment -Location <string> -TemplateParameterObject <hashtable>
 -TemplateSpecId <string> [-Name <string>] [-DeploymentDebugLogLevel <string>] [-Tag <hashtable>]
 [-WhatIfResultFormat <string>] [-WhatIfExcludeChangeType <string[]>] [-ProceedIfNoChange] [-AsJob]
 [-SkipTemplateParameterPrompt] [-QueryString <string>] [-ValidationLevel <string>] [-Pre]
 [-Profile <string>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### ByTemplateObjectAndParameterFile

```
New-AzTenantDeployment -Location <string> -TemplateParameterFile <string>
 -TemplateObject <hashtable> [-Name <string>] [-DeploymentDebugLogLevel <string>] [-Tag <hashtable>]
 [-WhatIfResultFormat <string>] [-WhatIfExcludeChangeType <string[]>] [-ProceedIfNoChange] [-AsJob]
 [-SkipTemplateParameterPrompt] [-QueryString <string>] [-ValidationLevel <string>] [-Pre]
 [-Profile <string>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### ByTemplateFileAndParameterFile

```
New-AzTenantDeployment -Location <string> -TemplateParameterFile <string> -TemplateFile <string>
 [-Name <string>] [-DeploymentDebugLogLevel <string>] [-Tag <hashtable>]
 [-WhatIfResultFormat <string>] [-WhatIfExcludeChangeType <string[]>] [-ProceedIfNoChange] [-AsJob]
 [-SkipTemplateParameterPrompt] [-QueryString <string>] [-ValidationLevel <string>] [-Pre]
 [-Profile <string>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### ByTemplateUriAndParameterFile

```
New-AzTenantDeployment -Location <string> -TemplateParameterFile <string> -TemplateUri <string>
 [-Name <string>] [-DeploymentDebugLogLevel <string>] [-Tag <hashtable>]
 [-WhatIfResultFormat <string>] [-WhatIfExcludeChangeType <string[]>] [-ProceedIfNoChange] [-AsJob]
 [-SkipTemplateParameterPrompt] [-QueryString <string>] [-ValidationLevel <string>] [-Pre]
 [-Profile <string>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### ByTemplateSpecResourceIdAndParams

```
New-AzTenantDeployment -Location <string> -TemplateParameterFile <string> -TemplateSpecId <string>
 [-Name <string>] [-DeploymentDebugLogLevel <string>] [-Tag <hashtable>]
 [-WhatIfResultFormat <string>] [-WhatIfExcludeChangeType <string[]>] [-ProceedIfNoChange] [-AsJob]
 [-SkipTemplateParameterPrompt] [-QueryString <string>] [-ValidationLevel <string>] [-Pre]
 [-Profile <string>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### ByParameterFileWithNoTemplate

```
New-AzTenantDeployment -Location <string> -TemplateParameterFile <string> [-Name <string>]
 [-DeploymentDebugLogLevel <string>] [-Tag <hashtable>] [-WhatIfResultFormat <string>]
 [-WhatIfExcludeChangeType <string[]>] [-ProceedIfNoChange] [-AsJob] [-SkipTemplateParameterPrompt]
 [-QueryString <string>] [-ValidationLevel <string>] [-Pre] [-Profile <string>] [-WhatIf] [-Confirm]
 [<CommonParameters>]
```

### ByTemplateObjectAndParameterUri

```
New-AzTenantDeployment -Location <string> -TemplateParameterUri <string> -TemplateObject <hashtable>
 [-Name <string>] [-DeploymentDebugLogLevel <string>] [-Tag <hashtable>]
 [-WhatIfResultFormat <string>] [-WhatIfExcludeChangeType <string[]>] [-ProceedIfNoChange] [-AsJob]
 [-SkipTemplateParameterPrompt] [-QueryString <string>] [-ValidationLevel <string>] [-Pre]
 [-Profile <string>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### ByTemplateFileAndParameterUri

```
New-AzTenantDeployment -Location <string> -TemplateParameterUri <string> -TemplateFile <string>
 [-Name <string>] [-DeploymentDebugLogLevel <string>] [-Tag <hashtable>]
 [-WhatIfResultFormat <string>] [-WhatIfExcludeChangeType <string[]>] [-ProceedIfNoChange] [-AsJob]
 [-SkipTemplateParameterPrompt] [-QueryString <string>] [-ValidationLevel <string>] [-Pre]
 [-Profile <string>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### ByTemplateUriAndParameterUri

```
New-AzTenantDeployment -Location <string> -TemplateParameterUri <string> -TemplateUri <string>
 [-Name <string>] [-DeploymentDebugLogLevel <string>] [-Tag <hashtable>]
 [-WhatIfResultFormat <string>] [-WhatIfExcludeChangeType <string[]>] [-ProceedIfNoChange] [-AsJob]
 [-SkipTemplateParameterPrompt] [-QueryString <string>] [-ValidationLevel <string>] [-Pre]
 [-Profile <string>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### ByTemplateSpecResourceIdAndParamsUri

```
New-AzTenantDeployment -Location <string> -TemplateParameterUri <string> -TemplateSpecId <string>
 [-Name <string>] [-DeploymentDebugLogLevel <string>] [-Tag <hashtable>]
 [-WhatIfResultFormat <string>] [-WhatIfExcludeChangeType <string[]>] [-ProceedIfNoChange] [-AsJob]
 [-SkipTemplateParameterPrompt] [-QueryString <string>] [-ValidationLevel <string>] [-Pre]
 [-Profile <string>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### ByTemplateObjectWithNoParameters

```
New-AzTenantDeployment -Location <string> -TemplateObject <hashtable> [-Name <string>]
 [-DeploymentDebugLogLevel <string>] [-Tag <hashtable>] [-WhatIfResultFormat <string>]
 [-WhatIfExcludeChangeType <string[]>] [-ProceedIfNoChange] [-AsJob] [-SkipTemplateParameterPrompt]
 [-QueryString <string>] [-ValidationLevel <string>] [-Pre] [-Profile <string>] [-WhatIf] [-Confirm]
 [<CommonParameters>]
```

### ByTemplateUriWithNoParameters

```
New-AzTenantDeployment -Location <string> -TemplateUri <string> [-Name <string>]
 [-DeploymentDebugLogLevel <string>] [-Tag <hashtable>] [-WhatIfResultFormat <string>]
 [-WhatIfExcludeChangeType <string[]>] [-ProceedIfNoChange] [-AsJob] [-SkipTemplateParameterPrompt]
 [-QueryString <string>] [-ValidationLevel <string>] [-Pre] [-Profile <string>] [-WhatIf] [-Confirm]
 [<CommonParameters>]
```

### ByTemplateSpecResourceId

```
New-AzTenantDeployment -Location <string> -TemplateSpecId <string> [-Name <string>]
 [-DeploymentDebugLogLevel <string>] [-Tag <hashtable>] [-WhatIfResultFormat <string>]
 [-WhatIfExcludeChangeType <string[]>] [-ProceedIfNoChange] [-AsJob] [-SkipTemplateParameterPrompt]
 [-QueryString <string>] [-ValidationLevel <string>] [-Pre] [-Profile <string>] [-WhatIf] [-Confirm]
 [<CommonParameters>]
```

## ALIASES

This cmdlet has no aliases.

## DESCRIPTION

Adds an Azure Resource Manager deployment at the current tenant scope, mirroring legacy `New-AzTenantDeployment`.
Specify exactly one template source (`-TemplateFile` for ARM JSON or Bicep, `-TemplateUri`, `-TemplateSpecId`, or `-TemplateObject`) and optionally template parameters as a parameter file, parameter URI, parameter object, or dynamic template parameters (for example `-environment prod`).
The template is validated before the deployment starts, the deployment runs in Incremental mode, and failed deployment operations are reported as errors while the final deployment is still returned.
Use `-WhatIf` to preview the resource changes, or `-Confirm` to preview and confirm before deploying. Use `-AsJob` to run the deployment in the background.

## EXAMPLES

### Example 1: Create a tenant deployment from a template file

Create a deployment at tenant scope from a local template file and parameter file.

```powershell
New-AzTenantDeployment -Name tenantBaseline -Location eastus -TemplateFile ./tenant.json -TemplateParameterFile ./tenant.parameters.json
```

### Example 2: Create a tenant deployment from a Bicep file with a parameter object

Template parameters can be passed as a hashtable.

```powershell
New-AzTenantDeployment -Location eastus -TemplateFile ./tenant.bicep -TemplateParameterObject @{ environment = 'prod' } -Tag @{ owner = 'platform' }
```

### Example 3: Preview a tenant deployment with What-If

Show the predicted resource changes without deploying.

```powershell
New-AzTenantDeployment -Location eastus -TemplateFile ./tenant.json -WhatIf -WhatIfExcludeChangeType NoChange,Ignore
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

The deployment debug log level.

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

The location to store deployment data.

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

The name of the deployment it's going to create. If not specified, defaults to the template file name when a template file is provided; defaults to the current time when a template object is provided, e.g. "20131223140835".

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

### -Pre

When set, indicates that the cmdlet should use pre-release API versions when automatically determining which version to use.

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

### -ProceedIfNoChange

Do not ask for confirmation if there is no changes in the What-If result. Applicable when the -Confirm switch is set.

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

Specifies a saved profile to use instead of the active profile.

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

The query string (for example, a SAS token) to be used with the TemplateUri parameter. Would be used in case of linked templates

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

Skips the PowerShell dynamic parameter processing that checks if the provided template parameter contains all necessary parameters used by the template. This check would prompt the user to provide a value for the missing parameters, but providing the -SkipTemplateParameterPrompt will ignore this prompt and error out immediately if a parameter was found not to be bound in the template. For non-interactive scripts, -SkipTemplateParameterPrompt can be provided to provide a better error message in the case where not all required parameters are satisfied.

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

### -Tag

The tags to put on the deployment.

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

Local path to the template file. Supported template file type: json and bicep.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: ByTemplateFileAndParameterObject
  Position: Named
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: true
  ValueFromRemainingArguments: false
- Name: ByTemplateFileAndParameterFile
  Position: Named
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: true
  ValueFromRemainingArguments: false
- Name: ByTemplateFileAndParameterUri
  Position: Named
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: true
  ValueFromRemainingArguments: false
- Name: ByTemplateFileWithNoParameters
  Position: Named
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: true
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
- Name: ByTemplateObjectAndParameterFile
  Position: Named
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: true
  ValueFromRemainingArguments: false
- Name: ByTemplateObjectAndParameterObject
  Position: Named
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: true
  ValueFromRemainingArguments: false
- Name: ByTemplateObjectAndParameterUri
  Position: Named
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: true
  ValueFromRemainingArguments: false
- Name: ByTemplateObjectWithNoParameters
  Position: Named
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: true
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -TemplateParameterFile

Parameter file to use for the template.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: ByTemplateObjectAndParameterFile
  Position: Named
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: true
  ValueFromRemainingArguments: false
- Name: ByTemplateFileAndParameterFile
  Position: Named
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: true
  ValueFromRemainingArguments: false
- Name: ByTemplateUriAndParameterFile
  Position: Named
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: true
  ValueFromRemainingArguments: false
- Name: ByTemplateSpecResourceIdAndParams
  Position: Named
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: true
  ValueFromRemainingArguments: false
- Name: ByParameterFileWithNoTemplate
  Position: Named
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: true
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -TemplateParameterObject

A hash table which represents the parameters.

```yaml
Type: System.Collections.Hashtable
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: ByTemplateObjectAndParameterObject
  Position: Named
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: true
  ValueFromRemainingArguments: false
- Name: ByTemplateFileAndParameterObject
  Position: Named
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: true
  ValueFromRemainingArguments: false
- Name: ByTemplateUriAndParameterObject
  Position: Named
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: true
  ValueFromRemainingArguments: false
- Name: ByTemplateSpecResourceIdAndParamsObject
  Position: Named
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: true
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -TemplateParameterUri

Uri to the template parameter file.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: ByTemplateObjectAndParameterUri
  Position: Named
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: true
  ValueFromRemainingArguments: false
- Name: ByTemplateFileAndParameterUri
  Position: Named
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: true
  ValueFromRemainingArguments: false
- Name: ByTemplateUriAndParameterUri
  Position: Named
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: true
  ValueFromRemainingArguments: false
- Name: ByTemplateSpecResourceIdAndParamsUri
  Position: Named
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: true
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -TemplateSpecId

Resource ID of the templateSpec to be deployed.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: ByTemplateSpecResourceId
  Position: Named
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: true
  ValueFromRemainingArguments: false
- Name: ByTemplateSpecResourceIdAndParamsUri
  Position: Named
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: true
  ValueFromRemainingArguments: false
- Name: ByTemplateSpecResourceIdAndParams
  Position: Named
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: true
  ValueFromRemainingArguments: false
- Name: ByTemplateSpecResourceIdAndParamsObject
  Position: Named
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: true
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -TemplateUri

Uri to the template file.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: ByTemplateUriAndParameterObject
  Position: Named
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: true
  ValueFromRemainingArguments: false
- Name: ByTemplateUriAndParameterFile
  Position: Named
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: true
  ValueFromRemainingArguments: false
- Name: ByTemplateUriAndParameterUri
  Position: Named
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: true
  ValueFromRemainingArguments: false
- Name: ByTemplateUriWithNoParameters
  Position: Named
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: true
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -ValidationLevel

Sets the validation level for validate/what-if. ValidationLevel can be Template(Skips provider validation), Provider(Performs full validation), or ProviderNoRbac(Performs full validation using RBAC read checks instead of RBAC write checks for provider validation).

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

Comma-separated resource change types to be excluded from What-If results. Applicable when the -WhatIf or -Confirm switch is set.

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

The What-If result format. Applicable when the -WhatIf or -Confirm switch is set.

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

### System.Collections.Hashtable

You can pipe a template object or parameter object to this cmdlet by property name.

### System.String

You can pipe template and parameter file paths or URIs to this cmdlet by property name.

## OUTPUTS

### Microsoft.Azure.Azclips.Extensions.Resources.Processors.PSDeployment

Returns the tenant deployment, including its provisioning state, parameters, and outputs.

## NOTES

Tenant deployments use API version 2025-04-01 and Incremental mode. Deploying at tenant scope requires permissions at the tenant root (`/`).

## RELATED LINKS

- [Azure Resource Manager tenant deployments](https://learn.microsoft.com/azure/azure-resource-manager/templates/deploy-to-tenant)
