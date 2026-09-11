---
document type: cmdlet
external help file: Microsoft.Azure.Azclips.Cmdlets.Deployment.dll-Help.xml
HelpUri: ''
Locale: en-US
Module Name: Deployment
ms.date: 09/11/2026
PlatyPS schema version: 2024-05-01
title: Get-AzManagementGroupDeployment
---

# Get-AzManagementGroupDeployment

## SYNOPSIS

Gets management-group-scoped deployments.

## SYNTAX

```
Get-AzManagementGroupDeployment -ManagementGroupId <string> [-Name <string>] [-Profile <string>]
Get-AzManagementGroupDeployment -Id <string> [-Profile <string>]
```

## DESCRIPTION

Lists deployments when Name is omitted, or gets one deployment by name or resource ID.

## EXAMPLES

```powershell
Get-AzManagementGroupDeployment -ManagementGroupId myManagementGroup
Get-AzManagementGroupDeployment -ManagementGroupId myManagementGroup -Name myDeployment
```
