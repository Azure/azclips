---
document type: cmdlet
external help file: Microsoft.Azure.Cirrus.Cmdlets.Login.dll-Help.xml
HelpUri: ''
Locale: en-AU
Module Name: Login
ms.date: 06/16/2026
PlatyPS schema version: 2024-05-01
title: Connect-CirrusAccount
---

# Connect-CirrusAccount

## SYNOPSIS

Authenticate to Azure.

## SYNTAX

### __AllParameterSets

```
Connect-CirrusAccount -SubscriptionId <string> [-TenantId <string>] [-UseDeviceCode] [-WhatIf]
 [-Confirm] [<CommonParameters>]
```

## ALIASES

This cmdlet has the following aliases,
  {{Insert list of aliases}}

## DESCRIPTION

Authenticate to Azure and persist account context for subsequent commands.

## EXAMPLES

### Sign in with the default browser flow

Launch the interactive browser sign-in and persist the resulting account context against subscription `00000000-0000-0000-0000-000000000000` for subsequent commands.

```powershell
Connect-CirrusAccount -SubscriptionId 00000000-0000-0000-0000-000000000000
```

### Sign in with device code on a host without a browser

Use the device-code flow when running on a remote shell, container, or any machine without an interactive browser. The cmdlet prints a code and URL to enter on a second device. Pass `-TenantId` to target a specific Entra tenant.

```powershell
Connect-CirrusAccount -SubscriptionId 00000000-0000-0000-0000-000000000000 -TenantId 11111111-1111-1111-1111-111111111111 -UseDeviceCode
```

## PARAMETERS

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

### -TenantId

Tenant ID to login to.

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

### -UseDeviceCode

Use device code flow for authentication.

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

### -WhatIf

Shows what would happen if the cmdlet runs.
The cmdlet is not run.

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

## NOTES

Generated help for login.


## RELATED LINKS


- [Online Version]()
