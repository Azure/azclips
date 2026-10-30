---
document type: cmdlet
external help file: Microsoft.Azure.Azclips.Cmdlets.Storage.dll-Help.xml
HelpUri: ''
Locale: en-US
Module Name: Storage
ms.date: 10/22/2026
PlatyPS schema version: 2024-05-01
title: Get-AzStorageBlobContent
---

# Get-AzStorageBlobContent

## SYNOPSIS

Download a blob to disk and return its native metadata.

## SYNTAX

### Name (Default)

```
Get-AzStorageBlobContent [-Blob] <string> [-Container] <string> [-Destination <string>]
 [-SnapshotTime <DateTimeOffset>] [-VersionId <string>] [-CheckMd5] [-ValidateContent]
 [-ConcurrentTaskCount <int>] [-ChunkSize <int>] [-Context <StorageContext>] [-Force]
 [-LeaseId <string>] [-IfMatch <string>] [-IfNoneMatch <string>] [-IfModifiedSince <DateTimeOffset>]
 [-IfUnmodifiedSince <DateTimeOffset>] [-TagCondition <string>] [-ServerTimeoutPerRequest <int>]
 [-Profile <string>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### Container

```
Get-AzStorageBlobContent [-Blob] <string> -ContainerObject <StorageBlobContainer>
 [-Destination <string>] [-SnapshotTime <DateTimeOffset>] [-VersionId <string>] [-CheckMd5]
 [-ValidateContent] [-ConcurrentTaskCount <int>] [-ChunkSize <int>] [-Context <StorageContext>]
 [-Force] [-LeaseId <string>] [-IfMatch <string>] [-IfNoneMatch <string>]
 [-IfModifiedSince <DateTimeOffset>] [-IfUnmodifiedSince <DateTimeOffset>] [-TagCondition <string>]
 [-ServerTimeoutPerRequest <int>] [-Profile <string>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### Blob

```
Get-AzStorageBlobContent -InputObject <StorageBlob> [-Destination <string>]
 [-SnapshotTime <DateTimeOffset>] [-VersionId <string>] [-CheckMd5] [-ValidateContent]
 [-ConcurrentTaskCount <int>] [-ChunkSize <int>] [-Context <StorageContext>] [-Force]
 [-LeaseId <string>] [-IfMatch <string>] [-IfNoneMatch <string>] [-IfModifiedSince <DateTimeOffset>]
 [-IfUnmodifiedSince <DateTimeOffset>] [-TagCondition <string>] [-ServerTimeoutPerRequest <int>]
 [-Profile <string>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### Uri

```
Get-AzStorageBlobContent -AbsoluteUri <string> [-Destination <string>]
 [-SnapshotTime <DateTimeOffset>] [-VersionId <string>] [-CheckMd5] [-ValidateContent]
 [-ConcurrentTaskCount <int>] [-ChunkSize <int>] [-Context <StorageContext>] [-Force]
 [-LeaseId <string>] [-IfMatch <string>] [-IfNoneMatch <string>] [-IfModifiedSince <DateTimeOffset>]
 [-IfUnmodifiedSince <DateTimeOffset>] [-TagCondition <string>] [-ServerTimeoutPerRequest <int>]
 [-Profile <string>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

## ALIASES

## DESCRIPTION

Uses Microsoft Entra credentials from a native Storage context against public
Azure. Destination defaults to the PowerShell current FileSystem directory.
An existing directory uses the blob's relative name and creates required
subdirectories only after confirmation. Unsafe traversal names are rejected.
Invalid filename characters are percent-escaped. A selected snapshot appends
` (yyyy-MM-dd HHmmss)` before the extension when naming beneath a directory.
An explicit destination filename is used unchanged.

An existing destination requires overwrite confirmation unless Force is set.
All confirmations run on the PowerShell invocation thread, before the engine
session. WhatIf performs local validation but no network or filesystem writes.
Without overwrite approval, a concurrent local create cannot be replaced.
The download pins all ranges to the source ETag. Errors are terminating and do
not produce success output.

## EXAMPLES

### Example 1: Download using the blob's relative name

```powershell
$context = New-AzStorageContext -StorageAccountName account123
Get-AzStorageBlobContent -Container reports -Blob 2026/report.txt -Context $context
```

### Example 2: Download a native selected blob to an explicit file

```powershell
$context = New-AzStorageContext -StorageAccountName account123
$blob = Get-AzStorageBlob -Container reports -Blob 2026/report.txt -Context $context
$blob | Get-AzStorageBlobContent -Destination .\download.bin -CheckMd5 -Force
```

## PARAMETERS

### -AbsoluteUri

Public Azure Blob URI containing only an optional snapshot or version selector.
Aliases: Uri, BlobUri. Authentication still uses Context; URI SAS authentication is
not supported for downloads.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases:
- Uri
- BlobUri
ParameterSets:
- Name: Uri
  Position: Named
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -Blob

Exact, literal blob name.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: Name
  Position: 0
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
- Name: Container
  Position: 0
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -CheckMd5

Validate downloaded ranges and compare the stored Content-MD5 when present.

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

One byte through 8 MiB; default 4 MiB. MD5-validated ranges are capped at 4 MiB.

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

One through four bounded workers; default two.

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

Request ShouldProcess confirmation independently of Force.
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

### -Container

Container name. Aliases: Name, N.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases:
- Name
- N
ParameterSets:
- Name: Name
  Position: 1
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -ContainerObject

Native container pipeline input. Alias: CloudBlobContainer.

```yaml
Type: Microsoft.Azure.Azclips.Extensions.Storage.Customization.StorageBlobContainer
DefaultValue: ''
SupportsWildcards: false
Aliases:
- CloudBlobContainer
ParameterSets:
- Name: Container
  Position: Named
  IsRequired: true
  ValueFromPipeline: true
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -Context

Native Storage context, unless inherited from a native resource.

```yaml
Type: Microsoft.Azure.Azclips.Extensions.Storage.Customization.StorageContext
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: (All)
  Position: Named
  IsRequired: false
  ValueFromPipeline: true
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -Destination

FileSystem file or existing directory. Defaults to the PowerShell location.
An explicit filename's parent must already exist. Alias: Path.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases:
- Path
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

Overwrite an existing local file without the additional overwrite prompt.

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

### -IfMatch

Source ETag match condition.

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

### -IfModifiedSince

Source modified-since condition.

```yaml
Type: System.Nullable`1[System.DateTimeOffset]
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

### -IfNoneMatch

Source ETag non-match condition.

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

### -IfUnmodifiedSince

Source unmodified-since condition.

```yaml
Type: System.Nullable`1[System.DateTimeOffset]
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

### -InputObject

Native blob pipeline input, including its snapshot or version selector.
Aliases: CloudBlob, ICloudBlob.

```yaml
Type: Microsoft.Azure.Azclips.Extensions.Storage.Customization.StorageBlob
DefaultValue: ''
SupportsWildcards: false
Aliases:
- CloudBlob
- ICloudBlob
ParameterSets:
- Name: Blob
  Position: Named
  IsRequired: true
  ValueFromPipeline: true
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -LeaseId

Source lease ID.

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

### -Profile

Named profile; must agree with the native context.

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

Positive service timeout, in seconds.

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

### -SnapshotTime

Exact snapshot timestamp. Cannot be combined with VersionId or conflict with
the selector of a native resource or URI.

```yaml
Type: System.Nullable`1[System.DateTimeOffset]
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

### -TagCondition

Source SQL-like blob tag condition.

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

### -ValidateContent

Validate each transfer range's transactional Content-MD5.

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

### -VersionId

Exact version selector.

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

Describe the download without authentication, network requests, or writes.
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

### Microsoft.Azure.Azclips.Extensions.Storage.Customization.StorageContext

### Microsoft.Azure.Azclips.Extensions.Storage.Customization.StorageBlobContainer

### Microsoft.Azure.Azclips.Extensions.Storage.Customization.StorageBlob

## OUTPUTS

### Microsoft.Azure.Azclips.Extensions.Storage.Customization.StorageBlob

## NOTES

## RELATED LINKS

[Get-AzStorageBlob](Get-AzStorageBlob.md)
