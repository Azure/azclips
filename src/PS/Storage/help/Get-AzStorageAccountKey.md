---
document type: cmdlet
external help file: Microsoft.Azure.Azclips.Cmdlets.Storage.dll-Help.xml
HelpUri: ''
Locale: en-US
Module Name: Storage
ms.date: 10/22/2026
PlatyPS schema version: 2024-05-01
title: Get-AzStorageAccountKey
---

# Get-AzStorageAccountKey

## SYNOPSIS

List the requested Storage account keys through ARM.

## SYNTAX

### __AllParameterSets

```
Get-AzStorageAccountKey -ResourceGroupName <string> -Name <string> [-SubscriptionId <string>]
 [-ListKerbKey] [-Profile <string>] [<CommonParameters>]
```

## ALIASES

None.

## DESCRIPTION

Calls ARM ListKeys for the named storage account using the selected public-Azure Entra profile and an explicit or profile-backed subscription. The identity needs permission to list account keys. ListKerbKey optionally requests Kerberos keys.

Key values are deliberately included in output and may be captured by the terminal or calling process. Protect that output. Service payloads and key values are excluded from failure diagnostics. All records are validated before any key is emitted.

This command is independent of data-plane contexts. Context creation, data-plane authentication and copy authorization never call it as an automatic authentication fallback.

## EXAMPLES

### Example 1

Explicitly request key records for an account in the selected profile's subscription.

```powershell
Get-AzStorageAccountKey -ResourceGroupName rg-storage -Name account123
```

## PARAMETERS

### -ListKerbKey

Include Kerberos keys by requesting ARM's kerb expansion. The default is no expansion.

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

### -Name

Storage account name: 3-24 lowercase letters or digits. AccountName and StorageAccountName are aliases.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases:
- AccountName
- StorageAccountName
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

Use the named repository connection profile, or the active profile when omitted. This is not a legacy DefaultProfile SDK object.

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

Resource group containing the storage account.

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

ARM subscription GUID. Omit to use the selected profile's subscription. An explicit value does not modify the profile.

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

## OUTPUTS

### Microsoft.Azure.Azclips.Extensions.Storage.Customization.StorageAccountKey

Enumerates native key records with KeyName, Value, Permissions and optional CreationTime. An empty service collection emits no records. Values are intentionally secret output; ToString does not print them.

## NOTES

Uses ARM API 2025-08-01 and the management.azure.com token audience. No data-plane StorageContext parameter is accepted. No key regeneration or key-based authentication is performed.

## RELATED LINKS

[New-AzStorageContext](New-AzStorageContext.md)
