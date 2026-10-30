---
document type: cmdlet
external help file: Microsoft.Azure.Azclips.Cmdlets.Storage.dll-Help.xml
HelpUri: ''
Locale: en-US
Module Name: Storage
ms.date: 10/22/2026
PlatyPS schema version: 2024-05-01
title: Set-AzStorageFileContent
---

# Set-AzStorageFileContent

## SYNOPSIS

Upload a local file to Azure Files, optionally returning the verified native file.

## SYNTAX

### ShareName (Default)

```
Set-AzStorageFileContent [-ShareName] <string> [-Source] <string> [[-Path] <string>] [-Force]
 [-PassThru] [-ValidateContent] [-ConcurrentTaskCount <int>] [-ChunkSize <int>]
 [-Metadata <hashtable>] [-ContentType <string>] [-ContentEncoding <string>]
 [-ContentLanguage <string>] [-ContentDisposition <string>] [-CacheControl <string>]
 [-ContentMD5 <byte[]>] [-Context <StorageContext>] [-EnableFileBackupRequestIntent]
 [-DisAllowTrailingDot] [-ShareSnapshot <string>] [-ServerTimeoutPerRequest <int>]
 [-Profile <string>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### Share

```
Set-AzStorageFileContent [-ShareObject] <StorageFileShare> [-Source] <string> [[-Path] <string>]
 [-Force] [-PassThru] [-ValidateContent] [-ConcurrentTaskCount <int>] [-ChunkSize <int>]
 [-Metadata <hashtable>] [-ContentType <string>] [-ContentEncoding <string>]
 [-ContentLanguage <string>] [-ContentDisposition <string>] [-CacheControl <string>]
 [-ContentMD5 <byte[]>] [-Context <StorageContext>] [-EnableFileBackupRequestIntent]
 [-DisAllowTrailingDot] [-ShareSnapshot <string>] [-ServerTimeoutPerRequest <int>]
 [-Profile <string>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### Directory

```
Set-AzStorageFileContent [-DirectoryObject] <StorageFileDirectory> [-Source] <string>
 [[-Path] <string>] [-Force] [-PassThru] [-ValidateContent] [-ConcurrentTaskCount <int>]
 [-ChunkSize <int>] [-Metadata <hashtable>] [-ContentType <string>] [-ContentEncoding <string>]
 [-ContentLanguage <string>] [-ContentDisposition <string>] [-CacheControl <string>]
 [-ContentMD5 <byte[]>] [-Context <StorageContext>] [-EnableFileBackupRequestIntent]
 [-DisAllowTrailingDot] [-ShareSnapshot <string>] [-ServerTimeoutPerRequest <int>]
 [-Profile <string>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

## ALIASES

None.


## DESCRIPTION

Uploads a local file using public-Azure Entra authentication with explicit Files backup intent. Supply a named
share and Context, or a native share/directory pipeline input. SDK resource objects, keys, SAS fallback and
alternate clouds are not accepted. Native resources supply their own context; explicit account/profile selections
must agree. ShareClient and ShareDirectoryClient are aliases for the native resource parameters.

Source is resolved using PowerShell filesystem-path semantics. An omitted/empty Path uses the source basename.
A trailing slash or existing remote directory appends the source basename; otherwise Path is an exact file name.
A directory probe returning 403 is treated as an exact-file selection, preserving the legacy write-only behavior.
Only a precise missing-resource response is treated as absence; missing shares, leases and other failures remain errors.
Snapshots cannot be uploaded into. Missing parent directories are created up to the share root.

Remote resolution and overwrite checks run in a read-only preflight. The typed preflight result returns after
engine-session disposal; ShouldProcess and any existing-file ShouldContinue prompt then run on the PowerShell
invocation thread, never on backend workers. The subsequent write uses the frozen exact account/profile/share/path,
content settings, MD5, metadata and transfer controls. Force suppresses only the overwrite prompt, never ShouldProcess.
WhatIf performs no requests or local writes; remote-directory disambiguation is deferred in its proposed target.
As with legacy Files upload, the file name is the overwrite scope, not an atomic ETag-conditioned transaction.

The transfer creates the file with its length, sends bounded parallel ranges and reports progress. Zero bytes
still creates a file. ValidateContent validates an MD5 for each uploaded range. A supplied ContentMD5, or a computed
whole-file MD5 when omitted, is stored after transfer with the selected content settings. Defaults use
application/octet-stream, inherited permissions, Archive attributes and current creation/last-write timestamps.
PassThru re-reads and verifies final properties before returning a native file. Default success emits no output.
Errors, cancellation and declined confirmation do not fabricate success; failures can leave a partial remote file.

## EXAMPLES

### Example 1

Upload using the source basename with an explicit native Entra context.

```powershell
$context = New-AzStorageContext -StorageAccountName account123 -EnableFileBackupRequestIntent
Set-AzStorageFileContent -ShareName documents -Context $context -Source .\month.csv -ValidateContent
```

### Example 2

Upload relative to a native directory with frozen content settings and metadata.

```powershell
$context = New-AzStorageContext -StorageAccountName account123 -EnableFileBackupRequestIntent
$directory = Get-AzStorageFile -ShareName documents -Context $context -Path reports
$directory | Set-AzStorageFileContent -Source .\month.csv -ContentType text/csv -Metadata @{ owner = 'finance' } -PassThru -Confirm
```

### Example 3

Preview, then authorize replacement without disabling standard confirmation.

```powershell
$context = New-AzStorageContext -StorageAccountName account123 -EnableFileBackupRequestIntent
Set-AzStorageFileContent -ShareName documents -Context $context -Source .\month.csv -Path reports/month.csv -WhatIf
Set-AzStorageFileContent -ShareName documents -Context $context -Source .\month.csv -Path reports/month.csv -Force -Confirm
```

## PARAMETERS

### -CacheControl

Stored cache-control header.

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

### -ChunkSize

Upload range size from one byte through 4 MiB.

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

One through four workers; default two.

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

Request ShouldProcess confirmation. Confirm:false does not suppress the separate overwrite ShouldContinue prompt.
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

### -ContentDisposition

Stored content-disposition header.

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

### -ContentEncoding

Stored content-encoding header.

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

### -ContentLanguage

Stored content-language header.

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

### -ContentMD5

Exactly 16 bytes of whole-file MD5. Omitted computes and stores a whole-file MD5 in source order.

```yaml
Type: System.Byte[]
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

### -ContentType

Stored content type; defaults to application/octet-stream.

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

### -DirectoryObject

Native destination parent. ShareDirectoryClient is an alias.

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

Explicitly authorize Files backup intent for this operation without mutating the supplied context.

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

### -Force

Skip the separate existing-file overwrite prompt. Does not bypass ShouldProcess or change the resolved target.

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

String-valued metadata with nonempty, case-insensitively unique keys; copied before confirmation.

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

### -PassThru

Return a verified native destination file only after successful completion.

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

Literal relative file/directory path. Defaults to the source basename; a trailing slash or existing directory
appends that basename. Remote directory disambiguation occurs during the read-only preflight.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: (All)
  Position: 2
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -Profile

Named public-Azure profile consistent with the selected context.

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

Positive service timeout in seconds for each request.

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

Destination share name used with Context.

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

Native destination share. ShareClient is an alias.

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

Consistent Files selector; uploads reject any share snapshot before sending requests.

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

### -Source

Local filesystem source resolved using PowerShell paths. FullName is an alias and binds by property name.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases:
- FullName
ParameterSets:
- Name: (All)
  Position: 1
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: true
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -ValidateContent

Compute and validate MD5 for each uploaded range; distinct from storing whole-file Content-MD5.

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

Show the proposed target without remote resolution or writes.
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

### System.String

Source can bind from a FullName or Source property.

## OUTPUTS

### Microsoft.Azure.Azclips.Extensions.Storage.Customization.StorageFile

Only with PassThru.

## NOTES

Requires appropriate Files backup-semantics RBAC. No recursive upload, job mode or SMB-attribute preservation.

## RELATED LINKS

- [New-AzStorageContext](New-AzStorageContext.md)
- [Get-AzStorageFile](Get-AzStorageFile.md)
- [Get-AzStorageFileContent](Get-AzStorageFileContent.md)
