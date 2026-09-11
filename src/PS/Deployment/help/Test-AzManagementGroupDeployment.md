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

```
Test-AzManagementGroupDeployment -ManagementGroupId <string> -Location <string> [-Name <string>]
 [-TemplateFile <string>] [-TemplateUri <string>] [-TemplateSpecId <string>]
 [-TemplateObject <hashtable>] [-QueryString <string>] [-TemplateParameterFile <string>]
 [-TemplateParameterUri <string>] [-TemplateParameterObject <hashtable>] [-ValidationLevel <string>]
 [-Profile <string>] [<CommonParameters>]
```

## DESCRIPTION

Validates a deployment without creating resources.

## EXAMPLES

```powershell
Test-AzManagementGroupDeployment -ManagementGroupId myManagementGroup -Location eastus -TemplateFile ./main.bicep
```
