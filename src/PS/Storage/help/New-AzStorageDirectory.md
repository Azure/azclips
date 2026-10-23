---
document type: cmdlet
external help file: Microsoft.Azure.Azclips.Cmdlets.Storage.dll-Help.xml
HelpUri: ''
Locale: en-US
Module Name: Storage
ms.date: 10/22/2026
PlatyPS schema version: 2024-05-01
title: New-AzStorageDirectory
---

# New-AzStorageDirectory

## SYNOPSIS

Create one Azure Files directory and return the confirmed native directory.

## SYNTAX

### ShareName (Default)

```
New-AzStorageDirectory [-ShareName] <string> [-Path] <string> [-Metadata <hashtable>]
 [-Context <StorageContext>] [-EnableFileBackupRequestIntent] [-DisAllowTrailingDot]
 [-ShareSnapshot <string>] [-ServerTimeoutPerRequest <int>] [-Profile <string>] [-WhatIf] [-Confirm]
 [<CommonParameters>]
```

### Share

```
New-AzStorageDirectory [-ShareObject] <StorageFileShare> [-Path] <string> [-Metadata <hashtable>]
 [-Context <StorageContext>] [-EnableFileBackupRequestIntent] [-DisAllowTrailingDot]
 [-ShareSnapshot <string>] [-ServerTimeoutPerRequest <int>] [-Profile <string>] [-WhatIf] [-Confirm]
 [<CommonParameters>]
```

### Directory

```
New-AzStorageDirectory [-DirectoryObject] <StorageFileDirectory> [-Path] <string>
 [-Metadata <hashtable>] [-Context <StorageContext>] [-EnableFileBackupRequestIntent]
 [-DisAllowTrailingDot] [-ShareSnapshot <string>] [-ServerTimeoutPerRequest <int>]
 [-Profile <string>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

## ALIASES

None.


## DESCRIPTION

Creates one directory in a live Azure Files share using public-Azure Entra authentication and explicit backup intent.
Supply a named share and Context, or a native share/directory pipeline input. Path is literal and relative to the
selected parent. Root and snapshot creation are rejected. The parent must already exist; this is not recursive.
Only HTTP 201 with valid service properties emits the native directory. Existing directories, missing parents,
authorization errors and other failures remain errors; success is never fabricated from an arbitrary conflict.

ShouldProcess runs on the invocation thread against the frozen exact target. WhatIf performs no remote request.
Metadata is copied before confirmation. Trailing dots are preserved unless DisAllowTrailingDot is present.
The accepted SMB defaults inherit permissions and set Directory attributes and current creation/last-write timestamps.

## EXAMPLES

### Example 1

Preview creating a child directory.

```powershell
$context = New-AzStorageContext -StorageAccountName account123 -EnableFileBackupRequestIntent
New-AzStorageDirectory -ShareName documents -Context $context -Path reports -WhatIf
```

### Example 2

Create a child relative to a native directory and return its confirmed properties.

```powershell
$context = New-AzStorageContext -StorageAccountName account123 -EnableFileBackupRequestIntent
$parent = Get-AzStorageFile -ShareName documents -Context $context -Path reports
$parent | New-AzStorageDirectory -Path october -Metadata @{ owner = 'finance' } -Confirm
```

## PARAMETERS

### -Confirm

Request standard ShouldProcess confirmation.
Prompts you for confirmation before running the cmdlet.
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

### -Context

Native Entra context. Required at runtime for ShareName input; native resources carry their own context.

```yaml
Type: Microsoft.Azure.Azclips.Extensions.Storage.Customization.StorageContext
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

### -DirectoryObject

Native parent directory. ShareDirectoryClient is an alias for this native type, not an SDK client.

```yaml
Type: Microsoft.Azure.Azclips.Extensions.Storage.Customization.StorageFileDirectory
DefaultValue: ''
SupportsWildcards: false
Aliases:
- ShareDirectoryClient
ParameterSets:
- Name: Directory
  Position: 0
  IsRequired: true
  ValueFromPipeline: true
  ValueFromPipelineByPropertyName: true
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -DisAllowTrailingDot

Disable preservation of trailing dots.

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

### -EnableFileBackupRequestIntent

Explicitly authorize Files backup request intent for this operation.

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

### -Metadata

String-valued metadata with nonempty, case-insensitively unique keys. Copied before ShouldProcess.

```yaml
Type: System.Collections.Hashtable
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

### -Path

Non-root literal directory path relative to the selected parent.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: (All)
  Position: 1
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -Profile

Named public-Azure profile; must agree with the selected context.

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

### -ServerTimeoutPerRequest

Positive per-request service timeout in seconds.

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

### -ShareName

Share name used with Context.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: ShareName
  Position: 0
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -ShareObject

Native share. ShareClient is an alias.

```yaml
Type: Microsoft.Azure.Azclips.Extensions.Storage.Customization.StorageFileShare
DefaultValue: ''
SupportsWildcards: false
Aliases:
- ShareClient
ParameterSets:
- Name: Share
  Position: 0
  IsRequired: true
  ValueFromPipeline: true
  ValueFromPipelineByPropertyName: true
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -ShareSnapshot

Snapshot selector is exposed consistently with other Files cmdlets, but creation in any snapshot is rejected.

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

### -WhatIf

Show the proposed operation without sending requests or writing resources.
Runs the command in a mode that only reports what would happen without performing the actions.
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

### Microsoft.Azure.Azclips.Extensions.Storage.Customization.StorageFileShare

### Microsoft.Azure.Azclips.Extensions.Storage.Customization.StorageFileDirectory

## OUTPUTS

### Microsoft.Azure.Azclips.Extensions.Storage.Customization.StorageFileDirectory

## NOTES

Requires Files backup-semantics RBAC. No keys, SAS fallback, alternate clouds or recursive parent creation.

## RELATED LINKS

- [New-AzStorageContext](New-AzStorageContext.md)
- [Get-AzStorageFile](Get-AzStorageFile.md)
