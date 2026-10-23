---
document type: cmdlet
external help file: Microsoft.Azure.Azclips.Cmdlets.Storage.dll-Help.xml
HelpUri: ''
Locale: en-US
Module Name: Storage
ms.date: 10/22/2026
PlatyPS schema version: 2024-05-01
title: Get-AzStorageFileContent
---

# Get-AzStorageFileContent

## SYNOPSIS

Download an Azure file to disk, optionally returning the verified native file.

## SYNTAX

### ShareName (Default)

```
Get-AzStorageFileContent [-ShareName] <string> [-Path] <string> [[-Destination] <string>] [-Force]
 [-PassThru] [-CheckMd5] [-ConcurrentTaskCount <int>] [-ChunkSize <int>] [-Context <StorageContext>]
 [-EnableFileBackupRequestIntent] [-DisAllowTrailingDot] [-ShareSnapshot <string>]
 [-ServerTimeoutPerRequest <int>] [-Profile <string>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### Share

```
Get-AzStorageFileContent [-ShareObject] <StorageFileShare> [-Path] <string>
 [[-Destination] <string>] [-Force] [-PassThru] [-CheckMd5] [-ConcurrentTaskCount <int>]
 [-ChunkSize <int>] [-Context <StorageContext>] [-EnableFileBackupRequestIntent]
 [-DisAllowTrailingDot] [-ShareSnapshot <string>] [-ServerTimeoutPerRequest <int>]
 [-Profile <string>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### Directory

```
Get-AzStorageFileContent [-DirectoryObject] <StorageFileDirectory> [-Path] <string>
 [[-Destination] <string>] [-Force] [-PassThru] [-CheckMd5] [-ConcurrentTaskCount <int>]
 [-ChunkSize <int>] [-Context <StorageContext>] [-EnableFileBackupRequestIntent]
 [-DisAllowTrailingDot] [-ShareSnapshot <string>] [-ServerTimeoutPerRequest <int>]
 [-Profile <string>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### File

```
Get-AzStorageFileContent [-FileObject] <StorageFile> [[-Destination] <string>] [-Force] [-PassThru]
 [-CheckMd5] [-ConcurrentTaskCount <int>] [-ChunkSize <int>] [-Context <StorageContext>]
 [-EnableFileBackupRequestIntent] [-DisAllowTrailingDot] [-ShareSnapshot <string>]
 [-ServerTimeoutPerRequest <int>] [-Profile <string>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

## ALIASES

None.


## DESCRIPTION

Downloads a literal Azure Files path using public-Azure Entra authentication and explicit Files backup intent.
Supply a named share and Context, a native share/directory plus relative Path, or a native file pipeline input.
Native resources retain their account/profile and share snapshot. SDK clients, key credentials and SAS fallback
are not accepted. ShareClient, ShareDirectoryClient and ShareFileClient are parameter aliases for native resources.

Destination defaults to the current PowerShell filesystem location. An existing local directory receives the
source's basename. An explicit filename is used literally, including when the source basename is not a valid
local Windows filename. The destination parent must exist. ShouldProcess and existing-file ShouldContinue
confirmation run only on PowerShell's invocation thread, before opening an engine session. Force bypasses only
the overwrite prompt, never ShouldProcess. WhatIf makes no requests or local writes. A destination that appears
after an absent-file preflight fails safely unless overwrite was authorized.

Transfers use bounded parallel ranges and report progress. CheckMd5 verifies returned range MD5 values and,
when available for the complete file, the stored Content-MD5. Missing stored whole-file MD5 does not invent a hash.
PassThru re-reads and verifies source properties before returning a native file. Default output is empty.
Errors, cancellation and declined confirmation never produce success output. Direct writes preserve legacy
filesystem/link behavior: failed downloads may leave partial local content.

## EXAMPLES

### Example 1

Download to an existing folder and validate checksums.

```powershell
$context = New-AzStorageContext -StorageAccountName account123 -EnableFileBackupRequestIntent
Get-AzStorageFileContent -ShareName documents -Path reports/month.csv -Context $context -Destination . -CheckMd5
```

### Example 2

Preview a native file download, then explicitly overwrite and return the native source.

```powershell
$context = New-AzStorageContext -StorageAccountName account123 -EnableFileBackupRequestIntent
$file = Get-AzStorageFile -ShareName documents -Path reports/month.csv -Context $context
$file | Get-AzStorageFileContent -Destination .\month.csv -WhatIf
$file | Get-AzStorageFileContent -Destination .\month.csv -Force -PassThru -Confirm
```

## PARAMETERS

### -CheckMd5

Validate per-range MD5 and stored whole-file MD5 when present.

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

### -ChunkSize

Range size from one byte through 8 MiB; checksum validation reduces effective ranges to at most 4 MiB.

```yaml
Type: System.Int32
DefaultValue: 4194304
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

### -ConcurrentTaskCount

One through four range workers; default two.

```yaml
Type: System.Int32
DefaultValue: 2
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

Request standard ShouldProcess confirmation. Confirm:false does not suppress the separate existing-file prompt.
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

Native Entra context; required at runtime for ShareName input and otherwise must agree with the native resource.

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

### -Destination

Local file or existing directory. Defaults to the current PowerShell filesystem location plus the source basename.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: ShareName
  Position: 2
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
- Name: Share
  Position: 2
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
- Name: Directory
  Position: 2
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
- Name: File
  Position: 1
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -DirectoryObject

Native source parent directory. ShareDirectoryClient is an alias.

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

Disable preservation of trailing dots in remote Files names.

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

Explicitly authorize Files backup intent for this operation without mutating the source context.

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

### -FileObject

Native file pipeline input. ShareFileClient and InputObject are aliases, not SDK object support.

```yaml
Type: Microsoft.Azure.Azclips.Extensions.Storage.Customization.StorageFile
DefaultValue: ''
SupportsWildcards: false
Aliases:
- ShareFileClient
- InputObject
ParameterSets:
- Name: File
  Position: 0
  IsRequired: true
  ValueFromPipeline: true
  ValueFromPipelineByPropertyName: true
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -Force

Authorize replacement of an existing local file without ShouldContinue. Does not bypass ShouldProcess.

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

### -PassThru

Return the verified native source file, not downloaded bytes, after a successful transfer.

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

### -Path

Literal relative source file path; required unless FileObject supplies the selected file.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: ShareName
  Position: 1
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
- Name: Share
  Position: 1
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
- Name: Directory
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

Named public-Azure profile consistent with the native source context.

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

Source share name, used with Context.

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

Native source share. ShareClient is an alias.

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

Read-only source share snapshot; must agree with a native snapshot selection.

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

Show the resolved local target without remote requests or local writes.
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

### Microsoft.Azure.Azclips.Extensions.Storage.Customization.StorageFile

## OUTPUTS

### Microsoft.Azure.Azclips.Extensions.Storage.Customization.StorageFile

Only with PassThru.

## NOTES

Requires appropriate Files backup-semantics RBAC. No stdout streaming, recursive download or job mode.

## RELATED LINKS

- [New-AzStorageContext](New-AzStorageContext.md)
- [Get-AzStorageFile](Get-AzStorageFile.md)
- [Set-AzStorageFileContent](Set-AzStorageFileContent.md)
