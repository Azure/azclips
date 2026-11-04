---
document type: cmdlet
external help file: Microsoft.Azure.Azclips.Cmdlets.Storage.dll-Help.xml
HelpUri: ''
Locale: en-US
Module Name: Storage
ms.date: 10/22/2026
PlatyPS schema version: 2024-05-01
title: Set-AzStorageBlobContent
---

# Set-AzStorageBlobContent

## SYNOPSIS

Upload a local file as a native Block, Page, or Append Blob.

## SYNTAX

### Name (Default)

```
Set-AzStorageBlobContent [-File] <string> -Container <string> [-Blob <string>] [-BlobType <string>]
 [-Properties <hashtable>] [-Metadata <hashtable>] [-Tag <hashtable>] [-StandardBlobTier <string>]
 [-PremiumPageBlobTier <string>] [-ConcurrentTaskCount <int>] [-ChunkSize <int>] [-ValidateContent]
 [-Context <StorageContext>] [-Force] [-LeaseId <string>] [-IfMatch <string>]
 [-IfNoneMatch <string>] [-IfModifiedSince <DateTimeOffset>] [-IfUnmodifiedSince <DateTimeOffset>]
 [-TagCondition <string>] [-ServerTimeoutPerRequest <int>] [-Profile <string>] [-WhatIf] [-Confirm]
 [<CommonParameters>]
```

### Container

```
Set-AzStorageBlobContent [-File] <string> -ContainerObject <StorageBlobContainer> [-Blob <string>]
 [-BlobType <string>] [-Properties <hashtable>] [-Metadata <hashtable>] [-Tag <hashtable>]
 [-StandardBlobTier <string>] [-PremiumPageBlobTier <string>] [-ConcurrentTaskCount <int>]
 [-ChunkSize <int>] [-ValidateContent] [-Context <StorageContext>] [-Force] [-LeaseId <string>]
 [-IfMatch <string>] [-IfNoneMatch <string>] [-IfModifiedSince <DateTimeOffset>]
 [-IfUnmodifiedSince <DateTimeOffset>] [-TagCondition <string>] [-ServerTimeoutPerRequest <int>]
 [-Profile <string>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### Blob

```
Set-AzStorageBlobContent [-File] <string> -InputObject <StorageBlob> [-BlobType <string>]
 [-Properties <hashtable>] [-Metadata <hashtable>] [-Tag <hashtable>] [-StandardBlobTier <string>]
 [-PremiumPageBlobTier <string>] [-ConcurrentTaskCount <int>] [-ChunkSize <int>] [-ValidateContent]
 [-Context <StorageContext>] [-Force] [-LeaseId <string>] [-IfMatch <string>]
 [-IfNoneMatch <string>] [-IfModifiedSince <DateTimeOffset>] [-IfUnmodifiedSince <DateTimeOffset>]
 [-TagCondition <string>] [-ServerTimeoutPerRequest <int>] [-Profile <string>] [-WhatIf] [-Confirm]
 [<CommonParameters>]
```

## ALIASES

## DESCRIPTION

Uses the selected profile's Microsoft Entra credentials against public Azure.
The default type is Block. When Blob is omitted, the local filename is used.
Page files and chunks must be 512-byte aligned. Append uploads replace the
destination; they do not append to an existing blob.

Local validation and ShouldProcess run before opening an execution session.
Without Force, an existing destination requires a separate overwrite confirmation
on the invocation thread, after disposing the discovery session. Approval pins
the replacement to the observed ETag. A missing destination is written with
If-None-Match `*`, preventing a concurrent create from being overwritten.
Force skips overwrite confirmation, not ShouldProcess or explicit conditions.
Cancellation or a declined prompt never submits a replacement.

## EXAMPLES

### Example 1: Upload using the local filename

```powershell
$context = New-AzStorageContext -StorageAccountName account123
Set-AzStorageBlobContent -File .\report.txt -Container reports -Context $context
```

### Example 2: Upload an aligned Page Blob through a native container

```powershell
$context = New-AzStorageContext -StorageAccountName account123
$container = (Get-AzStorageBlob -Container reports -Blob existing.txt -Context $context).Container
$container | Set-AzStorageBlobContent -File .\disk.vhd -BlobType Page -PremiumPageBlobTier P10
```

## PARAMETERS

### -Blob

Literal destination name. Defaults to the source filename.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: Name
  Position: Named
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
- Name: Container
  Position: Named
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -BlobType

Block (default), Page, or Append.

```yaml
Type: System.String
DefaultValue: Block
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
AcceptedValues:
- Block
- Page
- Append
HelpMessage: ''
```

### -ChunkSize

Chunk size in bytes, default 4 MiB. Block/Append allow up to 8 MiB;
Page allows up to 4 MiB and requires 512-byte alignment.

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

Destination container name. Aliases: Name, N.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases:
- Name
- N
ParameterSets:
- Name: Name
  Position: Named
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

Native Entra Storage context. Required unless supplied by a native resource.

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

### -File

Existing FileSystem source path. Relative paths use the PowerShell location.
Alias: FullName. In the Name parameter set, a piped FileInfo binds by FullName.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases:
- FullName
ParameterSets:
- Name: Name
  Position: 0
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: true
  ValueFromRemainingArguments: false
- Name: Container
  Position: 0
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
- Name: Blob
  Position: 0
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -Force

Allow replacement without the additional overwrite prompt.

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

Destination ETag match condition. An approved replacement must also match the
ETag observed during confirmation.

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

Destination modified-since condition.

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

Destination ETag non-match condition.

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

Destination unmodified-since condition.

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

Native base blob pipeline input. Aliases: CloudBlob, ICloudBlob. Snapshots and
versions are read-only and cannot be upload destinations.

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

Destination lease ID.

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

### -Metadata

String-keyed, string-valued metadata.

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

### -PremiumPageBlobTier

P4, P6, P10, P15, P20, P30, P40, P50, P60, P70, or P80. Applies to Page Blobs only.
Cannot be combined with StandardBlobTier.

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
AcceptedValues:
- P4
- P6
- P10
- P15
- P20
- P30
- P40
- P50
- P60
- P70
- P80
HelpMessage: ''
```

### -Profile

Named profile; must agree with the native destination context.

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

### -Properties

String-valued content settings: ContentType, ContentEncoding, ContentLanguage,
ContentDisposition, CacheControl, and ContentMD5 (Base64 encoding of 16 bytes).
ContentType defaults to an extension-based MIME type, or application/octet-stream.

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

### -StandardBlobTier

Hot, Cool, Cold, or Archive. Applies to Block Blobs only.

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
AcceptedValues:
- Hot
- Cool
- Cold
- Archive
HelpMessage: ''
```

### -Tag

Up to ten string-keyed, string-valued blob tags. Alias: Tags.

```yaml
Type: System.Collections.Hashtable
DefaultValue: ''
SupportsWildcards: false
Aliases:
- Tags
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

Destination SQL-like blob tag condition.

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

Validate transfer chunks using transactional Content-MD5.

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

Describe the operation without authentication or network requests.
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

### System.String

### Microsoft.Azure.Azclips.Extensions.Storage.Customization.StorageContext

### Microsoft.Azure.Azclips.Extensions.Storage.Customization.StorageBlobContainer

### Microsoft.Azure.Azclips.Extensions.Storage.Customization.StorageBlob

## OUTPUTS

### Microsoft.Azure.Azclips.Extensions.Storage.Customization.StorageBlob

## NOTES

## RELATED LINKS

[New-AzStorageContext](New-AzStorageContext.md)
