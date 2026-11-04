---
document type: cmdlet
external help file: Microsoft.Azure.Azclips.Cmdlets.Storage.dll-Help.xml
HelpUri: ''
Locale: en-US
Module Name: Storage
ms.date: 10/22/2026
PlatyPS schema version: 2024-05-01
title: New-AzStorageContext
---

# New-AzStorageContext

## SYNOPSIS

Create a public-Azure Storage context using a connected Entra profile.

## SYNTAX

### __AllParameterSets

```
New-AzStorageContext -StorageAccountName <string> [-UseConnectedAccount]
 [-EnableFileBackupRequestIntent] [-Profile <string>] [<CommonParameters>]
```

## ALIASES

None.

## DESCRIPTION

Constructs a native Azclips Storage context containing the account, selected profile and public Blob/File endpoints. Context construction makes no Storage or ARM request and does not retrieve account keys or persist access tokens. The account does not need an ARM subscription in its profile.

Entra authentication is the only supported mode. Shared Key, destination SAS, connection strings, anonymous access, sovereign clouds, custom endpoints and emulators are not supported. Explicitly enable backup intent before using this context with Azure Files.

## EXAMPLES

### Example 1

Create a context that explicitly enables Azure Files OAuth backup intent.

```powershell
$context = New-AzStorageContext -StorageAccountName account123 -EnableFileBackupRequestIntent
```

## PARAMETERS

### -EnableFileBackupRequestIntent

Explicitly enable backup intent for Azure Files OAuth. The default is false. This does not grant roles; the selected identity still needs the applicable service permissions.

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

Use the named repository connection profile. Omit to use the active profile. This is a profile name, not an Az.Storage DefaultProfile SDK object.

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

### -StorageAccountName

The existing public-Azure storage account name: 3-24 lowercase letters or digits.

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

### -UseConnectedAccount

Use the selected profile's Entra identity. This defaults to true even when the switch is omitted. Explicitly specifying -UseConnectedAccount:$false is unsupported and fails; no account-key fallback occurs.

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

### Microsoft.Azure.Azclips.Extensions.Storage.Customization.StorageContext

A native immutable Azclips context. It is not a legacy Az.Storage SDK context.

## NOTES

Only public Azure is supported. Context construction does not verify that the account exists or that the identity has data-plane permissions.

## RELATED LINKS
