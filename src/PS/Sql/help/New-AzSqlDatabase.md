---
document type: cmdlet
external help file: Microsoft.Azure.Azclips.Cmdlets.Sql.dll-Help.xml
HelpUri: ''
Locale: en-US
Module Name: Sql
ms.date: 08-25-2026
PlatyPS schema version: 2024-05-01
title: New-AzSqlDatabase
---

# New-AzSqlDatabase

## SYNOPSIS

Create a database in an Azure SQL server.

## SYNTAX

### __AllParameterSets

```
New-AzSqlDatabase -SubscriptionId <string> -ResourceGroupName <string> -ServerName <string>
 -DatabaseName <string> [-Edition <string>] [-RequestedServiceObjectiveName <string>]
 [-Family <string>] [-Capacity <int>] [-CollationName <string>] [-MaxSizeBytes <long>]
 [-ElasticPoolName <string>] [-ZoneRedundant] [-Profile <string>] [-WhatIf] [-Confirm]
 [<CommonParameters>]
```

## ALIASES

## DESCRIPTION

Creates a `Microsoft.Sql/servers/databases` resource under an existing logical SQL server.
A database inherits its region from its server, so no location is asked for: the cmdlet reads the parent server first and reuses its location.
Optionally sets the SKU (edition, service objective, family and capacity), the collation, the maximum size, elastic pool membership, and zone redundancy.
The create operation is long-running; the cmdlet waits for it to finish and returns the final database resource.

## EXAMPLES

### Create a database with the server's default settings

Provision a database named `mydb` on server `myserver001`, inheriting the server's region and the service default SKU.

```powershell
New-AzSqlDatabase -SubscriptionId 00000000-0000-0000-0000-000000000000 -ResourceGroupName myResourceGroup -ServerName myserver001 -DatabaseName mydb
```

### Create a Standard S0 database with an explicit collation and size cap

Pin the service tier and service objective, and cap the database at 250 GB.

```powershell
New-AzSqlDatabase -SubscriptionId 00000000-0000-0000-0000-000000000000 -ResourceGroupName myResourceGroup -ServerName myserver001 -DatabaseName mydb -Edition Standard -RequestedServiceObjectiveName S0 -CollationName SQL_Latin1_General_CP1_CI_AS -MaxSizeBytes 268435456000
```

### Create a zone-redundant General Purpose database on Gen5 hardware

Request two vCores on Gen5 hardware and spread the database across availability zones.

```powershell
New-AzSqlDatabase -SubscriptionId 00000000-0000-0000-0000-000000000000 -ResourceGroupName myResourceGroup -ServerName myserver001 -DatabaseName mydb -Edition GeneralPurpose -Family Gen5 -Capacity 2 -ZoneRedundant
```

### Create a database inside an existing elastic pool

Place the database in elastic pool `mypool` on the same server, so it draws from the pool's shared resources.

```powershell
New-AzSqlDatabase -SubscriptionId 00000000-0000-0000-0000-000000000000 -ResourceGroupName myResourceGroup -ServerName myserver001 -DatabaseName mydb -ElasticPoolName mypool
```

## PARAMETERS

### -Capacity

The capacity (vCores or DTUs) of the database SKU.

```yaml
Type: System.Nullable`1[System.Int32]
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

### -CollationName

The collation of the database.

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

### -DatabaseName

The name of the database.

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

### -Edition

The edition (service tier) of the database, for example GeneralPurpose or Standard.

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

### -ElasticPoolName

The name of the elastic pool, on the same server, to create the database in.

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

### -Family

The compute generation of the database SKU, for example Gen5.

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

### -MaxSizeBytes

The maximum size of the database in bytes.

```yaml
Type: System.Nullable`1[System.Int64]
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

Use the named profile for this command (overrides the active profile).

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

### -RequestedServiceObjectiveName

The service objective (SKU name) of the database, for example S0 or GP_Gen5_2.

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

### -ServerName

The name of the SQL server that hosts the database.

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

### -ZoneRedundant

Create the database as zone redundant.

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

### CommonParameters

This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable,
-InformationAction, -InformationVariable, -OutBuffer, -OutVariable, -PipelineVariable,
-ProgressAction, -Verbose, -WarningAction, and -WarningVariable. For more information, see
[about_CommonParameters](https://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

## OUTPUTS

### Microsoft.Azure.Azclips.Extensions.Sql.Models.SqlDatabase

The newly-created `SqlDatabase` object, including its assigned ARM resource ID, location, SKU, and current service objective.

## NOTES

Generated help for sql.

## RELATED LINKS

- [Online Version]()

