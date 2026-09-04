---
document type: cmdlet
external help file: Microsoft.Azure.Azclips.Cmdlets.Sql.dll-Help.xml
HelpUri: ''
Locale: en-US
Module Name: Sql
ms.date: 08-20-2026
PlatyPS schema version: 2024-05-01
title: New-AzSqlServer
---

# New-AzSqlServer

## SYNOPSIS

Create an Azure SQL logical server.

## SYNTAX

### __AllParameterSets

```
New-AzSqlServer -SubscriptionId <string> -ResourceGroupName <string> -Name <string>
 -Location <string> -AdministratorLogin <string> -AdministratorLoginPassword <string>
 [-ServerVersion <string>] [-MinimalTlsVersion <string>] [-PublicNetworkAccess <string>]
 [-Tags <hashtable>] [-Profile <string>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

## ALIASES

## DESCRIPTION

Creates a `Microsoft.Sql/servers` resource in the specified resource group and region, with the supplied administrator login and password.
Optionally pins the server version, the minimal TLS version the server enforces, whether public network access is allowed, and resource tags.
The create operation is long-running; the cmdlet waits for it to finish and returns the final server resource.

## EXAMPLES

### Create a SQL server in East US

Provision a logical server named `myserver001` in resource group `myResourceGroup`.
Assign `$adminPassword` from a secure source in your own pipeline before invoking the cmdlet (the value below is a literal placeholder, not a real password).

```powershell
$adminPassword = 'P@ssw0rd-replace-me!'
New-AzSqlServer -SubscriptionId 00000000-0000-0000-0000-000000000000 -ResourceGroupName myResourceGroup -Name myserver001 -Location eastus -AdministratorLogin sqladmin -AdministratorLoginPassword $adminPassword
```

### Create a SQL server with public network access disabled, TLS 1.2 enforced, and resource tags

Harden the server at creation time by blocking public endpoints and requiring TLS 1.2, and tag it for ownership tracking.

```powershell
$adminPassword = 'P@ssw0rd-replace-me!'
New-AzSqlServer -SubscriptionId 00000000-0000-0000-0000-000000000000 -ResourceGroupName myResourceGroup -Name myserver001 -Location eastus -AdministratorLogin sqladmin -AdministratorLoginPassword $adminPassword -ServerVersion 12.0 -MinimalTlsVersion 1.2 -PublicNetworkAccess Disabled -Tags @{ env = 'prod'; owner = 'data-team' }
```

## PARAMETERS

### -AdministratorLogin

The administrator login name.

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

### -AdministratorLoginPassword

The administrator login password.

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

### -Location

Location for the SQL server.

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

### -MinimalTlsVersion

Minimal TLS version enforced by the server.

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

### -Name

The name of the SQL server.

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

### -PublicNetworkAccess

Whether public network access is allowed.

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

### -ResourceGroupName

The resource group name.

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

### -ServerVersion

The version of the SQL server (e.g. 12.0).

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

Azure Subscription Id.

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

### -Tags

Resource tags as key/value pairs.

```yaml
Type: System.Collections.Hashtable
DefaultValue: ''
SupportsWildcards: false
Aliases:
- Tag
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

### Microsoft.Azure.Azclips.Extensions.Sql.Models.SqlServer

The newly-created `SqlServer` object, including its assigned ARM resource ID, state, fully-qualified domain name, and administrator login.

## NOTES

Generated help for sql.

## RELATED LINKS

- [Online Version]()

