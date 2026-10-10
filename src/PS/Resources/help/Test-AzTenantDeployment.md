---
document type: cmdlet
external help file: Microsoft.Azure.Azclips.Cmdlets.Resources.dll-Help.xml
HelpUri: ''
Locale: en-US
Module Name: Resources
ms.date: 10/10/2026
PlatyPS schema version: 2024-05-01
title: Test-AzTenantDeployment
---

# Test-AzTenantDeployment

## SYNOPSIS

Validates a deployment template and parameter values at tenant scope.

## SYNTAX

### ByTemplateFileWithNoParameters (Default)

```
Test-AzTenantDeployment -Location <string> -TemplateFile <string> [-Name <string>]
 [-SuppressDiagnostics] [-SkipTemplateParameterPrompt] [-QueryString <string>]
 [-ValidationLevel <string>] [-Pre] [-Profile <string>] [<CommonParameters>]
```

### ByTemplateObjectAndParameterObject

```
Test-AzTenantDeployment -Location <string> -TemplateParameterObject <hashtable>
 -TemplateObject <hashtable> [-Name <string>] [-SuppressDiagnostics] [-SkipTemplateParameterPrompt]
 [-QueryString <string>] [-ValidationLevel <string>] [-Pre] [-Profile <string>] [<CommonParameters>]
```

### ByTemplateFileAndParameterObject

```
Test-AzTenantDeployment -Location <string> -TemplateParameterObject <hashtable>
 -TemplateFile <string> [-Name <string>] [-SuppressDiagnostics] [-SkipTemplateParameterPrompt]
 [-QueryString <string>] [-ValidationLevel <string>] [-Pre] [-Profile <string>] [<CommonParameters>]
```

### ByTemplateUriAndParameterObject

```
Test-AzTenantDeployment -Location <string> -TemplateParameterObject <hashtable>
 -TemplateUri <string> [-Name <string>] [-SuppressDiagnostics] [-SkipTemplateParameterPrompt]
 [-QueryString <string>] [-ValidationLevel <string>] [-Pre] [-Profile <string>] [<CommonParameters>]
```

### ByTemplateSpecResourceIdAndParamsObject

```
Test-AzTenantDeployment -Location <string> -TemplateParameterObject <hashtable>
 -TemplateSpecId <string> [-Name <string>] [-SuppressDiagnostics] [-SkipTemplateParameterPrompt]
 [-QueryString <string>] [-ValidationLevel <string>] [-Pre] [-Profile <string>] [<CommonParameters>]
```

### ByTemplateObjectAndParameterFile

```
Test-AzTenantDeployment -Location <string> -TemplateParameterFile <string>
 -TemplateObject <hashtable> [-Name <string>] [-SuppressDiagnostics] [-SkipTemplateParameterPrompt]
 [-QueryString <string>] [-ValidationLevel <string>] [-Pre] [-Profile <string>] [<CommonParameters>]
```

### ByTemplateFileAndParameterFile

```
Test-AzTenantDeployment -Location <string> -TemplateParameterFile <string> -TemplateFile <string>
 [-Name <string>] [-SuppressDiagnostics] [-SkipTemplateParameterPrompt] [-QueryString <string>]
 [-ValidationLevel <string>] [-Pre] [-Profile <string>] [<CommonParameters>]
```

### ByTemplateUriAndParameterFile

```
Test-AzTenantDeployment -Location <string> -TemplateParameterFile <string> -TemplateUri <string>
 [-Name <string>] [-SuppressDiagnostics] [-SkipTemplateParameterPrompt] [-QueryString <string>]
 [-ValidationLevel <string>] [-Pre] [-Profile <string>] [<CommonParameters>]
```

### ByTemplateSpecResourceIdAndParams

```
Test-AzTenantDeployment -Location <string> -TemplateParameterFile <string> -TemplateSpecId <string>
 [-Name <string>] [-SuppressDiagnostics] [-SkipTemplateParameterPrompt] [-QueryString <string>]
 [-ValidationLevel <string>] [-Pre] [-Profile <string>] [<CommonParameters>]
```

### ByParameterFileWithNoTemplate

```
Test-AzTenantDeployment -Location <string> -TemplateParameterFile <string> [-Name <string>]
 [-SuppressDiagnostics] [-SkipTemplateParameterPrompt] [-QueryString <string>]
 [-ValidationLevel <string>] [-Pre] [-Profile <string>] [<CommonParameters>]
```

### ByTemplateObjectAndParameterUri

```
Test-AzTenantDeployment -Location <string> -TemplateParameterUri <string>
 -TemplateObject <hashtable> [-Name <string>] [-SuppressDiagnostics] [-SkipTemplateParameterPrompt]
 [-QueryString <string>] [-ValidationLevel <string>] [-Pre] [-Profile <string>] [<CommonParameters>]
```

### ByTemplateFileAndParameterUri

```
Test-AzTenantDeployment -Location <string> -TemplateParameterUri <string> -TemplateFile <string>
 [-Name <string>] [-SuppressDiagnostics] [-SkipTemplateParameterPrompt] [-QueryString <string>]
 [-ValidationLevel <string>] [-Pre] [-Profile <string>] [<CommonParameters>]
```

### ByTemplateUriAndParameterUri

```
Test-AzTenantDeployment -Location <string> -TemplateParameterUri <string> -TemplateUri <string>
 [-Name <string>] [-SuppressDiagnostics] [-SkipTemplateParameterPrompt] [-QueryString <string>]
 [-ValidationLevel <string>] [-Pre] [-Profile <string>] [<CommonParameters>]
```

### ByTemplateSpecResourceIdAndParamsUri

```
Test-AzTenantDeployment -Location <string> -TemplateParameterUri <string> -TemplateSpecId <string>
 [-Name <string>] [-SuppressDiagnostics] [-SkipTemplateParameterPrompt] [-QueryString <string>]
 [-ValidationLevel <string>] [-Pre] [-Profile <string>] [<CommonParameters>]
```

### ByTemplateObjectWithNoParameters

```
Test-AzTenantDeployment -Location <string> -TemplateObject <hashtable> [-Name <string>]
 [-SuppressDiagnostics] [-SkipTemplateParameterPrompt] [-QueryString <string>]
 [-ValidationLevel <string>] [-Pre] [-Profile <string>] [<CommonParameters>]
```

### ByTemplateUriWithNoParameters

```
Test-AzTenantDeployment -Location <string> -TemplateUri <string> [-Name <string>]
 [-SuppressDiagnostics] [-SkipTemplateParameterPrompt] [-QueryString <string>]
 [-ValidationLevel <string>] [-Pre] [-Profile <string>] [<CommonParameters>]
```

### ByTemplateSpecResourceId

```
Test-AzTenantDeployment -Location <string> -TemplateSpecId <string> [-Name <string>]
 [-SuppressDiagnostics] [-SkipTemplateParameterPrompt] [-QueryString <string>]
 [-ValidationLevel <string>] [-Pre] [-Profile <string>] [<CommonParameters>]
```

## ALIASES

This cmdlet has no aliases.

## DESCRIPTION

Determines whether a deployment template and its parameter values are valid at the current tenant scope, mirroring legacy `Test-AzTenantDeployment`.
Specify the template and parameters the same way as for `New-AzTenantDeployment`.
When the template is valid the cmdlet writes nothing (validation diagnostics are written as warnings unless `-SuppressDiagnostics` is set); when it is invalid the cmdlet returns the validation errors.

## EXAMPLES

### Example 1: Validate a tenant deployment

Validate a template file and parameter file at tenant scope.

```powershell
Test-AzTenantDeployment -Location eastus -TemplateFile ./tenant.json -TemplateParameterFile ./tenant.parameters.json
```

### Example 2: Validate a template object without diagnostics

Validate an in-memory template and hide validation diagnostics.

```powershell
$template = @{ '$schema' = 'https://schema.management.azure.com/schemas/2019-08-01/tenantDeploymentTemplate.json#'; contentVersion = '1.0.0.0'; resources = @() }
Test-AzTenantDeployment -Location eastus -TemplateObject $template -SuppressDiagnostics
```

## PARAMETERS

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

The name of the deployment it's going to test. If not specified, defaults to the template file name when a template file is provided

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

### -SuppressDiagnostics

When set, validation diagnostics will not be shown for valid deployments.

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

### Microsoft.Azure.Azclips.Extensions.Resources.Processors.PSResourceManagerError

Returns the validation errors when the template is invalid. Returns nothing when the template is valid.

## NOTES

Tenant deployments use API version 2025-04-01.

## RELATED LINKS

- [Azure Resource Manager tenant deployments](https://learn.microsoft.com/azure/azure-resource-manager/templates/deploy-to-tenant)
