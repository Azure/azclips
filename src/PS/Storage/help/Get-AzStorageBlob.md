---
document type: cmdlet
external help file: Microsoft.Azure.Azclips.Cmdlets.Storage.dll-Help.xml
HelpUri: ''
Locale: en-US
Module Name: Storage
ms.date: 10/22/2026
PlatyPS schema version: 2024-05-01
title: Get-AzStorageBlob
---

# Get-AzStorageBlob

## SYNOPSIS

Get a selected blob's properties or list native blobs.

## SYNTAX

### BlobName (Default)

```
Get-AzStorageBlob [[-Blob] <string>] [-Container] <string> -Context <StorageContext>
 [-IncludeDeleted] [-IncludeTag] [-MaxCount <int>]
 [-ContinuationToken <StorageBlobContinuationToken>] [-TagCondition <string>]
 [-ServerTimeoutPerRequest <int>] [-Profile <string>] [<CommonParameters>]
```

### BlobPrefix

```
Get-AzStorageBlob [-Container] <string> -Context <StorageContext> -Prefix <string> [-IncludeDeleted]
 [-IncludeVersion] [-IncludeTag] [-MaxCount <int>]
 [-ContinuationToken <StorageBlobContinuationToken>] [-ServerTimeoutPerRequest <int>]
 [-Profile <string>] [<CommonParameters>]
```

### SingleBlobSnapshotTime

```
Get-AzStorageBlob [-Blob] <string> [-Container] <string> -Context <StorageContext>
 -SnapshotTime <DateTimeOffset> [-IncludeTag] [-TagCondition <string>]
 [-ServerTimeoutPerRequest <int>] [-Profile <string>] [<CommonParameters>]
```

### SingleBlobVersionID

```
Get-AzStorageBlob [-Blob] <string> [-Container] <string> -Context <StorageContext>
 -VersionId <string> [-IncludeTag] [-TagCondition <string>] [-ServerTimeoutPerRequest <int>]
 [-Profile <string>] [<CommonParameters>]
```

### ContainerBlobName

```
Get-AzStorageBlob [[-Blob] <string>] -ContainerObject <StorageBlobContainer>
 [-Context <StorageContext>] [-IncludeDeleted] [-IncludeTag] [-MaxCount <int>]
 [-ContinuationToken <StorageBlobContinuationToken>] [-TagCondition <string>]
 [-ServerTimeoutPerRequest <int>] [-Profile <string>] [<CommonParameters>]
```

### ContainerBlobPrefix

```
Get-AzStorageBlob -ContainerObject <StorageBlobContainer> -Prefix <string>
 [-Context <StorageContext>] [-IncludeDeleted] [-IncludeVersion] [-IncludeTag] [-MaxCount <int>]
 [-ContinuationToken <StorageBlobContinuationToken>] [-ServerTimeoutPerRequest <int>]
 [-Profile <string>] [<CommonParameters>]
```

### ContainerBlobSnapshotTime

```
Get-AzStorageBlob [-Blob] <string> -ContainerObject <StorageBlobContainer>
 -SnapshotTime <DateTimeOffset> [-Context <StorageContext>] [-IncludeTag] [-TagCondition <string>]
 [-ServerTimeoutPerRequest <int>] [-Profile <string>] [<CommonParameters>]
```

### ContainerBlobVersionID

```
Get-AzStorageBlob [-Blob] <string> -ContainerObject <StorageBlobContainer> -VersionId <string>
 [-Context <StorageContext>] [-IncludeTag] [-TagCondition <string>] [-ServerTimeoutPerRequest <int>]
 [-Profile <string>] [<CommonParameters>]
```

### BlobObject

```
Get-AzStorageBlob -InputObject <StorageBlob> [-Context <StorageContext>] [-IncludeTag]
 [-TagCondition <string>] [-ServerTimeoutPerRequest <int>] [-Profile <string>] [<CommonParameters>]
```

## ALIASES

None.

## DESCRIPTION

Uses a public-Azure, profile-backed Entra StorageContext or a native resource carrying that context. An exact Blob name uses HEAD; IncludeTag additionally retrieves tags guarded by the observed ETag. SnapshotTime and VersionId select distinct exact resources. Missing or denied resources remain errors.

Omitting Blob, supplying a PowerShell wildcard pattern, or using Prefix selects flat listing. IncludeDeleted also selects listing, even for an exact name. Wildcard filtering uses PowerShell's case-insensitive semantics after the legacy literal-prefix optimization: the service prefix itself remains case-sensitive. Prefix is always literal, not a wildcard pattern. Listings include metadata, copy status and snapshots by default.

Lists stream native blobs with continuation information, not CLI JSON or marker warnings. MaxCount limits scanned entries before wildcard filtering, not the number of matching outputs. A later error terminates the operation but cannot retract already streamed records.

TagCondition applies only to exact lookup. Listing controls cannot be combined with snapshot/version selection. MaxCount and ContinuationToken on an unselected exact-name lookup are ignored, matching legacy behavior; use a pattern or Prefix when listing is intended. Native InputObject re-reads that blob and retains its selectors. Explicit context/profile inputs must agree with the native resource.

## EXAMPLES

### Example 1

Get a blob's properties and tags without downloading its content.

```powershell
$context = New-AzStorageContext -StorageAccountName account123
Get-AzStorageBlob -Container content -Blob 'reports/month.csv' -Context $context -IncludeTag
```

### Example 2

Filter names using PowerShell wildcards. MaxCount applies to entries scanned, including nonmatches.

```powershell
$context = New-AzStorageContext -StorageAccountName account123
Get-AzStorageBlob -Container content -Blob 'reports/*.csv' -Context $context -MaxCount 5000
```

### Example 3

Resume a prefix listing with the last emitted native token and the same listing inputs.

```powershell
$context = New-AzStorageContext -StorageAccountName account123
$page = @(Get-AzStorageBlob -Container content -Prefix 'reports/' -Context $context -MaxCount 5000)
$page
if ($page.Count -gt 0 -and $page[-1].ContinuationToken) {
    Get-AzStorageBlob -Container content -Prefix 'reports/' -Context $context -MaxCount 5000 -ContinuationToken $page[-1].ContinuationToken
}
```

### Example 4

Re-read native blob pipeline input, or list from its native container.

```powershell
$context = New-AzStorageContext -StorageAccountName account123
$blob = Get-AzStorageBlob -Container content -Blob 'reports/month.csv' -Context $context
$blob | Get-AzStorageBlob -IncludeTag
$blob.Container | Get-AzStorageBlob -Prefix 'reports/'
```

## PARAMETERS

### -Blob

Blob name or PowerShell wildcard pattern. Omit or use an empty string to list. Exact names use HEAD unless IncludeDeleted is selected. Snapshot/version sets always treat the name literally.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: true
Aliases: []
ParameterSets:
- Name: BlobName
  Position: 0
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
- Name: ContainerBlobName
  Position: 0
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
- Name: SingleBlobSnapshotTime
  Position: 0
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
- Name: SingleBlobVersionID
  Position: 0
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
- Name: ContainerBlobSnapshotTime
  Position: 0
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
- Name: ContainerBlobVersionID
  Position: 0
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -Container

Container name. Name and N are aliases for Container, not Blob. Required with a named Context; accepts pipeline properties.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases:
- N
- Name
ParameterSets:
- Name: BlobName
  Position: 1
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: true
  ValueFromRemainingArguments: false
- Name: BlobPrefix
  Position: 1
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: true
  ValueFromRemainingArguments: false
- Name: SingleBlobSnapshotTime
  Position: 1
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: true
  ValueFromRemainingArguments: false
- Name: SingleBlobVersionID
  Position: 1
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: true
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -ContainerObject

Native StorageBlobContainer, supplied directly or through the pipeline. Its account/profile replace the need for a separate Context.

```yaml
Type: Microsoft.Azure.Azclips.Extensions.Storage.Customization.StorageBlobContainer
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: ContainerBlobName
  Position: Named
  IsRequired: true
  ValueFromPipeline: true
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
- Name: ContainerBlobPrefix
  Position: Named
  IsRequired: true
  ValueFromPipeline: true
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
- Name: ContainerBlobSnapshotTime
  Position: Named
  IsRequired: true
  ValueFromPipeline: true
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
- Name: ContainerBlobVersionID
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

Native StorageContext from New-AzStorageContext. Required for named-container inputs and optional for native resource inputs, where it must agree with their context. Legacy SDK contexts, connection strings and destination SAS/key credentials are unsupported.

```yaml
Type: Microsoft.Azure.Azclips.Extensions.Storage.Customization.StorageContext
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: BlobName
  Position: Named
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
- Name: BlobPrefix
  Position: Named
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
- Name: SingleBlobSnapshotTime
  Position: Named
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
- Name: SingleBlobVersionID
  Position: Named
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
- Name: ContainerBlobName
  Position: Named
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
- Name: ContainerBlobPrefix
  Position: Named
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
- Name: ContainerBlobSnapshotTime
  Position: Named
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
- Name: ContainerBlobVersionID
  Position: Named
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
- Name: BlobObject
  Position: Named
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -ContinuationToken

Native token from a listed blob. Repeat the listing pattern/prefix and include options when resuming. Account, container, prefix and delimiter must match the token. Ignored for an unselected exact-name lookup; unavailable in selected snapshot/version or native blob-input sets.

```yaml
Type: Microsoft.Azure.Azclips.Extensions.Storage.Customization.StorageBlobContinuationToken
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: BlobName
  Position: Named
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
- Name: BlobPrefix
  Position: Named
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
- Name: ContainerBlobName
  Position: Named
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
- Name: ContainerBlobPrefix
  Position: Named
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -IncludeDeleted

Include deleted blobs when listing. With Blob, forces listing plus name filtering rather than HEAD. Cannot be combined with selected snapshots or versions.

```yaml
Type: System.Management.Automation.SwitchParameter
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: BlobName
  Position: Named
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
- Name: BlobPrefix
  Position: Named
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
- Name: ContainerBlobName
  Position: Named
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
- Name: ContainerBlobPrefix
  Position: Named
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -IncludeTag

Include tags in listing output, or make an ETag-guarded tag request after an exact HEAD. Defaults to false.

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

### -IncludeVersion

Include blob versions in a Prefix listing. Defaults to false; not accepted in name or snapshot/version sets.

```yaml
Type: System.Management.Automation.SwitchParameter
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: BlobPrefix
  Position: Named
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
- Name: ContainerBlobPrefix
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

Native StorageBlob pipeline input. Re-reads the exact resource using its context, name and snapshot/version selector. Legacy SDK objects are not accepted.

```yaml
Type: Microsoft.Azure.Azclips.Extensions.Storage.Customization.StorageBlob
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: BlobObject
  Position: Named
  IsRequired: true
  ValueFromPipeline: true
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -MaxCount

Maximum scanned entries before filtering, across service pages. Default is 2147483647; zero or negative means unlimited. Each service page is at most 5000 entries. Filtering can emit fewer records than MaxCount, or none; if every scanned entry is rejected, there is no output object carrying a continuation token. Use Prefix or a larger scan limit when this matters. Ignored for an unselected exact-name lookup; unavailable in selected snapshot/version or native blob-input sets.

```yaml
Type: System.Int32
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: BlobName
  Position: Named
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
- Name: BlobPrefix
  Position: Named
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
- Name: ContainerBlobName
  Position: Named
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
- Name: ContainerBlobPrefix
  Position: Named
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -Prefix

Literal, case-sensitive service prefix. Use an empty string to list the entire container. Cannot be combined with Blob or a snapshot/version selector.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: BlobPrefix
  Position: Named
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
- Name: ContainerBlobPrefix
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

Repository connection profile name, not a legacy DefaultProfile SDK object. If supplied, it must agree with the native context/resource profile.

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

Positive per-request service timeout in seconds. This does not replace command cancellation or impose an overall listing timeout.

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

Snapshot timestamp, converted to UTC with round-trip precision. Requires a nonempty exact Blob name. Cannot be combined with VersionId or listing controls.

```yaml
Type: System.DateTimeOffset
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: SingleBlobSnapshotTime
  Position: Named
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
- Name: ContainerBlobSnapshotTime
  Position: Named
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -TagCondition

SQL-like tag condition for exact HEAD and optional tag retrieval. Rejected for prefix/wildcard/deleted listing rather than silently ignored.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: BlobName
  Position: Named
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
- Name: SingleBlobSnapshotTime
  Position: Named
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
- Name: SingleBlobVersionID
  Position: Named
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
- Name: ContainerBlobName
  Position: Named
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
- Name: ContainerBlobSnapshotTime
  Position: Named
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
- Name: ContainerBlobVersionID
  Position: Named
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
- Name: BlobObject
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

Nonempty version ID selecting one exact Blob. Cannot be combined with SnapshotTime or listing controls.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: SingleBlobVersionID
  Position: Named
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
- Name: ContainerBlobVersionID
  Position: Named
  IsRequired: true
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

Container names can bind by pipeline property name.

### Microsoft.Azure.Azclips.Extensions.Storage.Customization.StorageBlobContainer

Native container input carrying its account/profile context.

### Microsoft.Azure.Azclips.Extensions.Storage.Customization.StorageBlob

Native selected blob input, re-read per pipeline record.

## OUTPUTS

### Microsoft.Azure.Azclips.Extensions.Storage.Customization.StorageBlob

Native blob properties, metadata, optional tags and selectors. Listing records carry their service-page continuation token; a completed listing has no next token. Empty listings emit no records. Blob content and access tokens are not returned.

## NOTES

Uses Blob REST API 2026-06-06 and the Storage Entra audience. No automatic ARM key lookup, Shared Key fallback, inline content or legacy SDK object compatibility. Copy-source credentials and raw descriptions remain redacted.

## RELATED LINKS

[New-AzStorageContext](New-AzStorageContext.md)

[Remove-AzStorageBlob](Remove-AzStorageBlob.md)
