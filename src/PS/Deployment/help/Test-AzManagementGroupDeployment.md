---
document type: cmdlet
external help file: Microsoft.Azure.Azclips.Cmdlets.Deployment.dll-Help.xml
HelpUri: ''
Locale: en-US
Module Name: Deployment
ms.date: 09/11/2026
PlatyPS schema version: 2024-05-01
title: Test-AzManagementGroupDeployment
---

# Test-AzManagementGroupDeployment

## SYNOPSIS

Validates an ARM or Bicep deployment at management-group scope.

## SYNTAX

### __AllParameterSets

```
Test-AzManagementGroupDeployment -ManagementGroupId <string> -Location <string> [-Name <string>]
 [-TemplateFile <string>] [-TemplateUri <string>] [-TemplateSpecId <string>]
 [-TemplateObject <hashtable>] [-QueryString <string>] [-TemplateParameterFile <string>]
 [-TemplateParameterUri <string>] [-TemplateParameterObject <hashtable>] [-ValidationLevel <string>]
 [-Profile <string>] [<CommonParameters>]
```

## ALIASES

This cmdlet has no aliases.

## DESCRIPTION

Validates a deployment without creating resources.

## EXAMPLES

### Validate a local Bicep template

Validate a local Bicep template at management-group scope without creating resources.

```powershell
Test-AzManagementGroupDeployment -ManagementGroupId myManagementGroup -Location eastus -TemplateFile ./main.bicep
```

## PARAMETERS
