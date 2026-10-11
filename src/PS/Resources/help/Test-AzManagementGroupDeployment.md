---
document type: cmdlet
external help file: Microsoft.Azure.Azclips.Cmdlets.Resources.dll-Help.xml
HelpUri: ''
Locale: en-US
Module Name: Resources
ms.date: 10/12/2026
PlatyPS schema version: 2024-05-01
title: Test-AzManagementGroupDeployment
---

# Test-AzManagementGroupDeployment

## SYNOPSIS

Validates a management-group deployment.

## SYNTAX

### __AllParameterSets

```
Test-AzManagementGroupDeployment -ManagementGroupId <string> -Location <string> [-Name <string>]
 [-TemplateFile <string>] [-TemplateUri <string>] [-TemplateSpecId <string>]
 [-TemplateObject <hashtable>] [-QueryString <string>] [-TemplateParameterFile <string>]
 [-TemplateParameterUri <string>] [-TemplateParameterObject <hashtable>] [-Parameter <string[]>]
 [-ValidationLevel <string>] [-SuppressDiagnostics] [-SkipTemplateParameterPrompt] [-Pre]
 [-Profile <string>] [<CommonParameters>]
```

### ByTemplateFileWithNoParameters (Default)

```
Test-AzManagementGroupDeployment -ManagementGroupId <string> -Location <string>
 -TemplateFile <string> [-Name <string>] [-SuppressDiagnostics] [-SkipTemplateParameterPrompt]
 [-QueryString <string>] [-ValidationLevel <string>] [-Pre] [-Profile <string>] [<CommonParameters>]
```

### ByTemplateObjectAndParameterObject

```
Test-AzManagementGroupDeployment -ManagementGroupId <string> -Location <string>
 -TemplateParameterObject <hashtable> -TemplateObject <hashtable> [-Name <string>]
 [-SuppressDiagnostics] [-SkipTemplateParameterPrompt] [-QueryString <string>]
 [-ValidationLevel <string>] [-Pre] [-Profile <string>] [<CommonParameters>]
```

### ByTemplateFileAndParameterObject

```
Test-AzManagementGroupDeployment -ManagementGroupId <string> -Location <string>
 -TemplateParameterObject <hashtable> -TemplateFile <string> [-Name <string>] [-SuppressDiagnostics]
 [-SkipTemplateParameterPrompt] [-QueryString <string>] [-ValidationLevel <string>] [-Pre]
 [-Profile <string>] [<CommonParameters>]
```

### ByTemplateUriAndParameterObject

```
Test-AzManagementGroupDeployment -ManagementGroupId <string> -Location <string>
 -TemplateParameterObject <hashtable> -TemplateUri <string> [-Name <string>] [-SuppressDiagnostics]
 [-SkipTemplateParameterPrompt] [-QueryString <string>] [-ValidationLevel <string>] [-Pre]
 [-Profile <string>] [<CommonParameters>]
```

### ByTemplateSpecResourceIdAndParamsObject

```
Test-AzManagementGroupDeployment -ManagementGroupId <string> -Location <string>
 -TemplateParameterObject <hashtable> -TemplateSpecId <string> [-Name <string>]
 [-SuppressDiagnostics] [-SkipTemplateParameterPrompt] [-QueryString <string>]
 [-ValidationLevel <string>] [-Pre] [-Profile <string>] [<CommonParameters>]
```

### ByTemplateObjectAndParameterFile

```
Test-AzManagementGroupDeployment -ManagementGroupId <string> -Location <string>
 -TemplateParameterFile <string> -TemplateObject <hashtable> [-Name <string>] [-SuppressDiagnostics]
 [-SkipTemplateParameterPrompt] [-QueryString <string>] [-ValidationLevel <string>] [-Pre]
 [-Profile <string>] [<CommonParameters>]
```

### ByTemplateFileAndParameterFile

```
Test-AzManagementGroupDeployment -ManagementGroupId <string> -Location <string>
 -TemplateParameterFile <string> -TemplateFile <string> [-Name <string>] [-SuppressDiagnostics]
 [-SkipTemplateParameterPrompt] [-QueryString <string>] [-ValidationLevel <string>] [-Pre]
 [-Profile <string>] [<CommonParameters>]
```

### ByTemplateUriAndParameterFile

```
Test-AzManagementGroupDeployment -ManagementGroupId <string> -Location <string>
 -TemplateParameterFile <string> -TemplateUri <string> [-Name <string>] [-SuppressDiagnostics]
 [-SkipTemplateParameterPrompt] [-QueryString <string>] [-ValidationLevel <string>] [-Pre]
 [-Profile <string>] [<CommonParameters>]
```

### ByTemplateSpecResourceIdAndParams

```
Test-AzManagementGroupDeployment -ManagementGroupId <string> -Location <string>
 -TemplateParameterFile <string> -TemplateSpecId <string> [-Name <string>] [-SuppressDiagnostics]
 [-SkipTemplateParameterPrompt] [-QueryString <string>] [-ValidationLevel <string>] [-Pre]
 [-Profile <string>] [<CommonParameters>]
```

### ByParameterFileWithNoTemplate

```
Test-AzManagementGroupDeployment -ManagementGroupId <string> -Location <string>
 -TemplateParameterFile <string> [-Name <string>] [-SuppressDiagnostics]
 [-SkipTemplateParameterPrompt] [-QueryString <string>] [-ValidationLevel <string>] [-Pre]
 [-Profile <string>] [<CommonParameters>]
```

### ByTemplateObjectAndParameterUri

```
Test-AzManagementGroupDeployment -ManagementGroupId <string> -Location <string>
 -TemplateParameterUri <string> -TemplateObject <hashtable> [-Name <string>] [-SuppressDiagnostics]
 [-SkipTemplateParameterPrompt] [-QueryString <string>] [-ValidationLevel <string>] [-Pre]
 [-Profile <string>] [<CommonParameters>]
```

### ByTemplateFileAndParameterUri

```
Test-AzManagementGroupDeployment -ManagementGroupId <string> -Location <string>
 -TemplateParameterUri <string> -TemplateFile <string> [-Name <string>] [-SuppressDiagnostics]
 [-SkipTemplateParameterPrompt] [-QueryString <string>] [-ValidationLevel <string>] [-Pre]
 [-Profile <string>] [<CommonParameters>]
```

### ByTemplateUriAndParameterUri

```
Test-AzManagementGroupDeployment -ManagementGroupId <string> -Location <string>
 -TemplateParameterUri <string> -TemplateUri <string> [-Name <string>] [-SuppressDiagnostics]
 [-SkipTemplateParameterPrompt] [-QueryString <string>] [-ValidationLevel <string>] [-Pre]
 [-Profile <string>] [<CommonParameters>]
```

### ByTemplateSpecResourceIdAndParamsUri

```
Test-AzManagementGroupDeployment -ManagementGroupId <string> -Location <string>
 -TemplateParameterUri <string> -TemplateSpecId <string> [-Name <string>] [-SuppressDiagnostics]
 [-SkipTemplateParameterPrompt] [-QueryString <string>] [-ValidationLevel <string>] [-Pre]
 [-Profile <string>] [<CommonParameters>]
```

### ByTemplateObjectWithNoParameters

```
Test-AzManagementGroupDeployment -ManagementGroupId <string> -Location <string>
 -TemplateObject <hashtable> [-Name <string>] [-SuppressDiagnostics] [-SkipTemplateParameterPrompt]
 [-QueryString <string>] [-ValidationLevel <string>] [-Pre] [-Profile <string>] [<CommonParameters>]
```

### ByTemplateUriWithNoParameters

```
Test-AzManagementGroupDeployment -ManagementGroupId <string> -Location <string>
 -TemplateUri <string> [-Name <string>] [-SuppressDiagnostics] [-SkipTemplateParameterPrompt]
 [-QueryString <string>] [-ValidationLevel <string>] [-Pre] [-Profile <string>] [<CommonParameters>]
```

### ByTemplateSpecResourceId

```
Test-AzManagementGroupDeployment -ManagementGroupId <string> -Location <string>
 -TemplateSpecId <string> [-Name <string>] [-SuppressDiagnostics] [-SkipTemplateParameterPrompt]
 [-QueryString <string>] [-ValidationLevel <string>] [-Pre] [-Profile <string>] [<CommonParameters>]
```

## ALIASES

This cmdlet has no aliases.


## DESCRIPTION

Builds the same Incremental scoped request as New and sends it to ARM validation without
creating resources.

## EXAMPLES

### Example 1

Validates a local Bicep template at management-group scope.

```powershell
Test-AzManagementGroupDeployment -ManagementGroupId myManagementGroup -Location eastus -TemplateFile ./main.bicep
```

## PARAMETERS

### -Location

The location used to store deployment metadata.
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

### -ManagementGroupId

The management group ID.

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

The deployment name. When omitted, the template file name or current UTC time is used.
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

### -Parameter

Inline `KEY=VALUE` template parameter overlays.

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

### -Pre

Accepted for compatibility with Azure PowerShell. Deployment cmdlets use a fixed API version, so this switch has no effect.
When set, indicates that the cmdlet should use pre-release API versions when automatically determining which version to use.

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

### -Profile

The AzClips profile to use instead of the active profile.

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

A query string used with linked template or parameter URIs.
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

Skips creating dynamic parameters from the template parameters, so PowerShell does not prompt for missing values. Missing required values are reported by the deployment validation instead.
Skips the PowerShell dynamic parameter processing that checks if the provided template parameter contains all necessary parameters used by the template. This check would prompt the user to provide a value for the missing parameters, but providing the -SkipTemplateParameterPrompt will ignore this prompt and error out immediately if a parameter was found not to be bound in the template. For non-interactive scripts, -SkipTemplateParameterPrompt can be provided to provide a better error message in the case where not all required parameters are satisfied.

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

### -SuppressDiagnostics

Hides the validation diagnostics warning for a valid deployment.
When set, validation diagnostics will not be shown for valid deployments.

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

### -TemplateFile

A local ARM JSON or Bicep template file.
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

An inline template hashtable.
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

A local ARM parameters JSON or bicepparam file.
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

An inline template parameter hashtable.
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

A remote template parameter file URI.
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

The resource ID of a template spec version.
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

A remote ARM template URI.
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

The validation level: `Template`, `Provider`, or `ProviderNoRbac`.
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

Validation does not create or update resources.

## RELATED LINKS


- [Azure Resource Manager deployments](https://learn.microsoft.com/azure/azure-resource-manager/templates/deploy-to-management-group)
