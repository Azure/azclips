---
document type: cmdlet
external help file: Microsoft.Azure.Azclips.Cmdlets.Sql.dll-Help.xml
HelpUri: ''
Locale: en-US
Module Name: Sql
ms.date: 08-28-2026
PlatyPS schema version: 2024-05-01
title: New-AzSqlServerFirewallRule
---

# New-AzSqlServerFirewallRule

## SYNOPSIS

Create a firewall rule on an Azure SQL server.

## SYNTAX

### UseIpAddressRange (Default)

```
New-AzSqlServerFirewallRule -SubscriptionId <string> -ResourceGroupName <string>
 -ServerName <string> -FirewallRuleName <string> -StartIpAddress <string> -EndIpAddress <string>
 [-Profile <string>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### AllowAllAzureIPs

```
New-AzSqlServerFirewallRule -SubscriptionId <string> -ResourceGroupName <string>
 -ServerName <string> -AllowAllAzureIPs [-Profile <string>] [-WhatIf] [-Confirm]
 [<CommonParameters>]
```

## ALIASES

## DESCRIPTION

Creates a `Microsoft.Sql/servers/firewallRules` resource on an existing logical SQL server.
A firewall rule opens an inclusive IPv4 range on the server's public endpoint; use the same value for the start and end address to allow a single client address.
A firewall rule is a proxy resource, so it has no region of its own and no location is asked for.
`-AllowAllAzureIPs` is a shortcut for the well-known `AllowAllWindowsAzureIps` rule, which uses `0.0.0.0` as both the start and the end address - the ARM convention for all Azure-internal IP addresses rather than a literal address.
The request is a create-or-update, so re-running it with a different range updates the existing rule.
The operation is synchronous, so the cmdlet returns the stored rule immediately.
Addresses are validated by the service, which rejects malformed addresses and ranges whose end address is lower than the start address.

## EXAMPLES

### Allow a single client IP address

Open one address on the server's public endpoint by using the same value for the start and end of the range.

```powershell
New-AzSqlServerFirewallRule -SubscriptionId 00000000-0000-0000-0000-000000000000 -ResourceGroupName myResourceGroup -ServerName myserver001 -FirewallRuleName AllowMyIp -StartIpAddress 203.0.113.5 -EndIpAddress 203.0.113.5
```

### Allow a range of office IP addresses

Open an inclusive range so every address between the two endpoints can reach the server.

```powershell
New-AzSqlServerFirewallRule -SubscriptionId 00000000-0000-0000-0000-000000000000 -ResourceGroupName myResourceGroup -ServerName myserver001 -FirewallRuleName AllowOffice -StartIpAddress 203.0.113.5 -EndIpAddress 203.0.113.10
```

### Allow all Azure services to reach the server

Create the well-known `AllowAllWindowsAzureIps` rule without spelling out the rule name or the `0.0.0.0` addresses.

```powershell
New-AzSqlServerFirewallRule -SubscriptionId 00000000-0000-0000-0000-000000000000 -ResourceGroupName myResourceGroup -ServerName myserver001 -AllowAllAzureIPs
```

## PARAMETERS

### -AllowAllAzureIPs

Create the well-known `AllowAllWindowsAzureIps` rule, which allows all Azure-internal IP addresses (0.0.0.0-0.0.0.0).

```yaml
Type: System.Management.Automation.SwitchParameter
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: AllowAllAzureIPs
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

### -EndIpAddress

The end IP address of the firewall rule, in IPv4 format.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: UseIpAddressRange
  Position: Named
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -FirewallRuleName

The name of the firewall rule.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: UseIpAddressRange
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

The name of the SQL server the firewall rule applies to.

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

### -StartIpAddress

The start IP address of the firewall rule, in IPv4 format.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: UseIpAddressRange
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

### CommonParameters

This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable,
-InformationAction, -InformationVariable, -OutBuffer, -OutVariable, -PipelineVariable,
-ProgressAction, -Verbose, -WarningAction, and -WarningVariable. For more information, see
[about_CommonParameters](https://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

## OUTPUTS

### Microsoft.Azure.Azclips.Extensions.Sql.Models.SqlFirewallRule

The newly-created `SqlFirewallRule` object, including its assigned ARM resource ID, name, and the stored start and end IP addresses.

## NOTES

Generated help for sql.

## RELATED LINKS

- [Online Version]()
