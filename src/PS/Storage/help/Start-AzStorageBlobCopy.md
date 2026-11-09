---
document type: cmdlet
external help file: Microsoft.Azure.Azclips.Cmdlets.Storage.dll-Help.xml
HelpUri: ''
Locale: en-US
Module Name: Storage
ms.date: 11/06/2026
PlatyPS schema version: 2024-05-01
title: Start-AzStorageBlobCopy
---

# Start-AzStorageBlobCopy

## SYNOPSIS

Start an asynchronous Blob or Files-to-Blob copy and return a native destination blob.

## SYNTAX

### NamedBlob (Default)

```
Start-AzStorageBlobCopy [-SrcBlob] <string> -SrcContainer <string> [-DestContainer <string>]
 [-DestBlob <string>] [-DestContainerObject <StorageBlobContainer>] [-DestBlobObject <StorageBlob>]
 [-DestContext <StorageContext>] [-SrcContext <StorageContext>] [-SnapshotTime <DateTimeOffset>]
 [-VersionId <string>] [-SourceShareSnapshot <string>] [-SourceSas <string>]
 [-SourceAccountKey <string>] [-UseSourceAccountKeyLookup] [-SourceKeyResourceGroup <string>]
 [-SourceKeySubscriptionId <string>] [-SourceKeyName <string>]
 [-EnableSourceFileBackupRequestIntent] [-SourceIfMatch <string>] [-SourceIfNoneMatch <string>]
 [-SourceIfModifiedSince <DateTimeOffset>] [-SourceIfUnmodifiedSince <DateTimeOffset>]
 [-TagCondition <string>] [-DestTagCondition <string>] [-Metadata <hashtable>] [-Tag <hashtable>]
 [-StandardBlobTier <string>] [-PremiumPageBlobTier <string>] [-RehydratePriority <string>]
 [-Context <StorageContext>] [-Force] [-LeaseId <string>] [-IfMatch <string>]
 [-IfNoneMatch <string>] [-IfModifiedSince <DateTimeOffset>] [-IfUnmodifiedSince <DateTimeOffset>]
 [-ServerTimeoutPerRequest <int>] [-Profile <string>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### NativeBlob

```
Start-AzStorageBlobCopy -InputObject <StorageBlob> [-DestContainer <string>] [-DestBlob <string>]
 [-DestContainerObject <StorageBlobContainer>] [-DestBlobObject <StorageBlob>]
 [-DestContext <StorageContext>] [-SrcContext <StorageContext>] [-SnapshotTime <DateTimeOffset>]
 [-VersionId <string>] [-SourceShareSnapshot <string>] [-SourceSas <string>]
 [-SourceAccountKey <string>] [-UseSourceAccountKeyLookup] [-SourceKeyResourceGroup <string>]
 [-SourceKeySubscriptionId <string>] [-SourceKeyName <string>]
 [-EnableSourceFileBackupRequestIntent] [-SourceIfMatch <string>] [-SourceIfNoneMatch <string>]
 [-SourceIfModifiedSince <DateTimeOffset>] [-SourceIfUnmodifiedSince <DateTimeOffset>]
 [-TagCondition <string>] [-DestTagCondition <string>] [-Metadata <hashtable>] [-Tag <hashtable>]
 [-StandardBlobTier <string>] [-PremiumPageBlobTier <string>] [-RehydratePriority <string>]
 [-Context <StorageContext>] [-Force] [-LeaseId <string>] [-IfMatch <string>]
 [-IfNoneMatch <string>] [-IfModifiedSince <DateTimeOffset>] [-IfUnmodifiedSince <DateTimeOffset>]
 [-ServerTimeoutPerRequest <int>] [-Profile <string>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### NativeContainer

```
Start-AzStorageBlobCopy [-SrcBlob] <string> -ContainerObject <StorageBlobContainer>
 [-DestContainer <string>] [-DestBlob <string>] [-DestContainerObject <StorageBlobContainer>]
 [-DestBlobObject <StorageBlob>] [-DestContext <StorageContext>] [-SrcContext <StorageContext>]
 [-SnapshotTime <DateTimeOffset>] [-VersionId <string>] [-SourceShareSnapshot <string>]
 [-SourceSas <string>] [-SourceAccountKey <string>] [-UseSourceAccountKeyLookup]
 [-SourceKeyResourceGroup <string>] [-SourceKeySubscriptionId <string>] [-SourceKeyName <string>]
 [-EnableSourceFileBackupRequestIntent] [-SourceIfMatch <string>] [-SourceIfNoneMatch <string>]
 [-SourceIfModifiedSince <DateTimeOffset>] [-SourceIfUnmodifiedSince <DateTimeOffset>]
 [-TagCondition <string>] [-DestTagCondition <string>] [-Metadata <hashtable>] [-Tag <hashtable>]
 [-StandardBlobTier <string>] [-PremiumPageBlobTier <string>] [-RehydratePriority <string>]
 [-Context <StorageContext>] [-Force] [-LeaseId <string>] [-IfMatch <string>]
 [-IfNoneMatch <string>] [-IfModifiedSince <DateTimeOffset>] [-IfUnmodifiedSince <DateTimeOffset>]
 [-ServerTimeoutPerRequest <int>] [-Profile <string>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### Uri

```
Start-AzStorageBlobCopy -AbsoluteUri <string> [-DestContainer <string>] [-DestBlob <string>]
 [-DestContainerObject <StorageBlobContainer>] [-DestBlobObject <StorageBlob>]
 [-DestContext <StorageContext>] [-SrcContext <StorageContext>] [-SnapshotTime <DateTimeOffset>]
 [-VersionId <string>] [-SourceShareSnapshot <string>] [-SourceSas <string>]
 [-SourceAccountKey <string>] [-UseSourceAccountKeyLookup] [-SourceKeyResourceGroup <string>]
 [-SourceKeySubscriptionId <string>] [-SourceKeyName <string>]
 [-EnableSourceFileBackupRequestIntent] [-SourceIfMatch <string>] [-SourceIfNoneMatch <string>]
 [-SourceIfModifiedSince <DateTimeOffset>] [-SourceIfUnmodifiedSince <DateTimeOffset>]
 [-TagCondition <string>] [-DestTagCondition <string>] [-Metadata <hashtable>] [-Tag <hashtable>]
 [-StandardBlobTier <string>] [-PremiumPageBlobTier <string>] [-RehydratePriority <string>]
 [-Context <StorageContext>] [-Force] [-LeaseId <string>] [-IfMatch <string>]
 [-IfNoneMatch <string>] [-IfModifiedSince <DateTimeOffset>] [-IfUnmodifiedSince <DateTimeOffset>]
 [-ServerTimeoutPerRequest <int>] [-Profile <string>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### NamedFile

```
Start-AzStorageBlobCopy -SrcShareName <string> -SrcFilePath <string> [-DestContainer <string>]
 [-DestBlob <string>] [-DestContainerObject <StorageBlobContainer>] [-DestBlobObject <StorageBlob>]
 [-DestContext <StorageContext>] [-SrcContext <StorageContext>] [-SnapshotTime <DateTimeOffset>]
 [-VersionId <string>] [-SourceShareSnapshot <string>] [-SourceSas <string>]
 [-SourceAccountKey <string>] [-UseSourceAccountKeyLookup] [-SourceKeyResourceGroup <string>]
 [-SourceKeySubscriptionId <string>] [-SourceKeyName <string>]
 [-EnableSourceFileBackupRequestIntent] [-SourceIfMatch <string>] [-SourceIfNoneMatch <string>]
 [-SourceIfModifiedSince <DateTimeOffset>] [-SourceIfUnmodifiedSince <DateTimeOffset>]
 [-TagCondition <string>] [-DestTagCondition <string>] [-Metadata <hashtable>] [-Tag <hashtable>]
 [-StandardBlobTier <string>] [-PremiumPageBlobTier <string>] [-RehydratePriority <string>]
 [-Context <StorageContext>] [-Force] [-LeaseId <string>] [-IfMatch <string>]
 [-IfNoneMatch <string>] [-IfModifiedSince <DateTimeOffset>] [-IfUnmodifiedSince <DateTimeOffset>]
 [-ServerTimeoutPerRequest <int>] [-Profile <string>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### NativeShare

```
Start-AzStorageBlobCopy -SrcShare <StorageFileShare> -SrcFilePath <string> [-DestContainer <string>]
 [-DestBlob <string>] [-DestContainerObject <StorageBlobContainer>] [-DestBlobObject <StorageBlob>]
 [-DestContext <StorageContext>] [-SrcContext <StorageContext>] [-SnapshotTime <DateTimeOffset>]
 [-VersionId <string>] [-SourceShareSnapshot <string>] [-SourceSas <string>]
 [-SourceAccountKey <string>] [-UseSourceAccountKeyLookup] [-SourceKeyResourceGroup <string>]
 [-SourceKeySubscriptionId <string>] [-SourceKeyName <string>]
 [-EnableSourceFileBackupRequestIntent] [-SourceIfMatch <string>] [-SourceIfNoneMatch <string>]
 [-SourceIfModifiedSince <DateTimeOffset>] [-SourceIfUnmodifiedSince <DateTimeOffset>]
 [-TagCondition <string>] [-DestTagCondition <string>] [-Metadata <hashtable>] [-Tag <hashtable>]
 [-StandardBlobTier <string>] [-PremiumPageBlobTier <string>] [-RehydratePriority <string>]
 [-Context <StorageContext>] [-Force] [-LeaseId <string>] [-IfMatch <string>]
 [-IfNoneMatch <string>] [-IfModifiedSince <DateTimeOffset>] [-IfUnmodifiedSince <DateTimeOffset>]
 [-ServerTimeoutPerRequest <int>] [-Profile <string>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### NativeDirectory

```
Start-AzStorageBlobCopy -SrcDir <StorageFileDirectory> -SrcFilePath <string>
 [-DestContainer <string>] [-DestBlob <string>] [-DestContainerObject <StorageBlobContainer>]
 [-DestBlobObject <StorageBlob>] [-DestContext <StorageContext>] [-SrcContext <StorageContext>]
 [-SnapshotTime <DateTimeOffset>] [-VersionId <string>] [-SourceShareSnapshot <string>]
 [-SourceSas <string>] [-SourceAccountKey <string>] [-UseSourceAccountKeyLookup]
 [-SourceKeyResourceGroup <string>] [-SourceKeySubscriptionId <string>] [-SourceKeyName <string>]
 [-EnableSourceFileBackupRequestIntent] [-SourceIfMatch <string>] [-SourceIfNoneMatch <string>]
 [-SourceIfModifiedSince <DateTimeOffset>] [-SourceIfUnmodifiedSince <DateTimeOffset>]
 [-TagCondition <string>] [-DestTagCondition <string>] [-Metadata <hashtable>] [-Tag <hashtable>]
 [-StandardBlobTier <string>] [-PremiumPageBlobTier <string>] [-RehydratePriority <string>]
 [-Context <StorageContext>] [-Force] [-LeaseId <string>] [-IfMatch <string>]
 [-IfNoneMatch <string>] [-IfModifiedSince <DateTimeOffset>] [-IfUnmodifiedSince <DateTimeOffset>]
 [-ServerTimeoutPerRequest <int>] [-Profile <string>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### NativeFile

```
Start-AzStorageBlobCopy -SrcFile <StorageFile> [-DestContainer <string>] [-DestBlob <string>]
 [-DestContainerObject <StorageBlobContainer>] [-DestBlobObject <StorageBlob>]
 [-DestContext <StorageContext>] [-SrcContext <StorageContext>] [-SnapshotTime <DateTimeOffset>]
 [-VersionId <string>] [-SourceShareSnapshot <string>] [-SourceSas <string>]
 [-SourceAccountKey <string>] [-UseSourceAccountKeyLookup] [-SourceKeyResourceGroup <string>]
 [-SourceKeySubscriptionId <string>] [-SourceKeyName <string>]
 [-EnableSourceFileBackupRequestIntent] [-SourceIfMatch <string>] [-SourceIfNoneMatch <string>]
 [-SourceIfModifiedSince <DateTimeOffset>] [-SourceIfUnmodifiedSince <DateTimeOffset>]
 [-TagCondition <string>] [-DestTagCondition <string>] [-Metadata <hashtable>] [-Tag <hashtable>]
 [-StandardBlobTier <string>] [-PremiumPageBlobTier <string>] [-RehydratePriority <string>]
 [-Context <StorageContext>] [-Force] [-LeaseId <string>] [-IfMatch <string>]
 [-IfNoneMatch <string>] [-IfModifiedSince <DateTimeOffset>] [-IfUnmodifiedSince <DateTimeOffset>]
 [-ServerTimeoutPerRequest <int>] [-Profile <string>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

## ALIASES

## DESCRIPTION

Uses public Azure endpoints and Microsoft Entra profiles. Context is the source
context and, unless overridden, the destination context. SrcContext explicitly
selects a separate source context; DestContext selects the destination. Native
resources retain their own contexts and reject conflicting explicit contexts.
Unsigned Blob sources in the same account and profile use destination Entra
authorization without requesting a user delegation key. Other unsigned Blob
sources obtain short-lived read-only user-delegation authorization using the
source profile. Files sources require an existing service/account SAS,
a supplied SourceAccountKey, or explicit UseSourceAccountKeyLookup with
SourceKeyResourceGroup. SourceKeyName defaults to key1; SourceKeySubscriptionId
defaults to the source profile subscription. ARM lookup uses the source profile,
not the destination profile. Keys are used only to sign a read-only file SAS
valid for one day, with a five-minute start-time allowance.

A SAS supplied in AbsoluteUri or SourceSas takes precedence over source-key
options. A failed or invalid SAS never triggers key lookup or another
authorization mode. Keys and authorized source URLs are not persisted or
included in returned metadata. Do not log credential values.

The destination blob name defaults to the source blob name, or the source file's
filename. Copy destinations must be base blobs, not snapshots or versions.
The Files source account must permit Shared Key authorization. The destination
continues to use Entra authentication and can keep Shared Key disabled. Files
user-delegation SAS is not supported for normal asynchronous Blob copy because
the service does not forward its required backup-intent header. Backup intent
alone does not authorize a normal-copy Files source.

Without Force an existing destination requires an overwrite confirmation.
Discovery completes and its session is disposed before the cmdlet prompts on
the invocation thread. Approval pins the destination ETag; a missing destination
uses If-None-Match `*`. Source authorization and copy submission do not run if
overwrite is declined. Force bypasses only the additional overwrite prompt.

TagCondition applies to source Blob tags, matching Azure PowerShell.
SourceTagCondition is an alias for TagCondition. DestTagCondition applies to
destination Blob tags. Source tag conditions cannot be used with Files sources.

The returned native blob contains the acknowledged copy ID and status.
Pending, success, failed, and aborted states are preserved. This command never
polls for completion and does not claim that pending copy content is available.

## EXAMPLES

### Example 1: Copy across separate source and destination accounts

```powershell
$sourceContext = New-AzStorageContext -StorageAccountName sourceaccount
$destinationContext = New-AzStorageContext -StorageAccountName destaccount
Start-AzStorageBlobCopy -SrcContainer reports -SrcBlob report.txt -Context $sourceContext `
  -DestContainer archive -DestContext $destinationContext
```

### Example 2: Explicitly authorize ARM key lookup for a native Files source

```powershell
$sourceContext = New-AzStorageContext -StorageAccountName sourceaccount -EnableFileBackupRequestIntent
$destinationContext = New-AzStorageContext -StorageAccountName destaccount
$file = Get-AzStorageFile -ShareName reports -Path report.txt -Context $sourceContext
$file | Start-AzStorageBlobCopy -DestContainer archive -DestContext $destinationContext `
  -UseSourceAccountKeyLookup -SourceKeyResourceGroup source-rg
```

### Example 3: Use a supplied source SAS or source account key

```powershell
$sourceContext = New-AzStorageContext -StorageAccountName sourceaccount
$destinationContext = New-AzStorageContext -StorageAccountName destaccount
Start-AzStorageBlobCopy -SrcShareName reports -SrcFilePath report.txt -SrcContext $sourceContext `
  -DestContainer archive -DestContext $destinationContext -SourceSas $env:SOURCE_FILE_SAS
Start-AzStorageBlobCopy -SrcShareName reports -SrcFilePath report.txt -SrcContext $sourceContext `
  -DestContainer archive -DestBlob keyed-copy.txt -DestContext $destinationContext `
  -SourceAccountKey $env:SOURCE_STORAGE_KEY
```

## PARAMETERS

### -AbsoluteUri

Public Azure Blob/Files source URI. Aliases: SrcUri, SourceUri.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases:
- SrcUri
- SourceUri
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

### -Confirm

Request ShouldProcess confirmation independently of Force.
Prompts you for confirmation before running the cmdlet.
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

### -ContainerObject

Native source container. Aliases: CloudBlobContainer, SourceCloudBlobContainer.

```yaml
Type: Microsoft.Azure.Azclips.Extensions.Storage.Customization.StorageBlobContainer
DefaultValue: ''
SupportsWildcards: false
Aliases:
- CloudBlobContainer
- SourceCloudBlobContainer
ParameterSets:
- Name: NativeContainer
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

Source context; also the default destination context.

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

### -DestBlob

Destination name. Alias: DestinationBlob. Defaults to the source name.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases:
- DestinationBlob
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

### -DestBlobObject

Native base destination blob. Aliases: DestCloudBlob, DestinationCloudBlob,
DestICloudBlob, DestinationICloudBlob.

```yaml
Type: Microsoft.Azure.Azclips.Extensions.Storage.Customization.StorageBlob
DefaultValue: ''
SupportsWildcards: false
Aliases:
- DestCloudBlob
- DestinationCloudBlob
- DestICloudBlob
- DestinationICloudBlob
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

### -DestContainer

Destination container name. Alias: DestinationContainer.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases:
- DestinationContainer
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

### -DestContainerObject

Native destination container. Alias: DestinationContainerObject.

```yaml
Type: Microsoft.Azure.Azclips.Extensions.Storage.Customization.StorageBlobContainer
DefaultValue: ''
SupportsWildcards: false
Aliases:
- DestinationContainerObject
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

### -DestContext

Explicit destination context. Alias: DestinationContext.

```yaml
Type: Microsoft.Azure.Azclips.Extensions.Storage.Customization.StorageContext
DefaultValue: ''
SupportsWildcards: false
Aliases:
- DestinationContext
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

### -DestTagCondition

Destination SQL-like tag condition.

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

### -EnableSourceFileBackupRequestIntent

Retain explicit Files OAuth backup intent. This switch alone does not authorize
a normal-copy source; supply a Files service/account SAS or select source-key signing.

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

Allow destination replacement without an additional overwrite prompt.

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

Destination ETag match condition; also constrained by the confirmed ETag.

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

Native source blob, including its selectors. Aliases: CloudBlob, SrcCloudBlob,
SourceCloudBlob, ICloudBlob, SrcICloudBlob, SourceICloudBlob.

```yaml
Type: Microsoft.Azure.Azclips.Extensions.Storage.Customization.StorageBlob
DefaultValue: ''
SupportsWildcards: false
Aliases:
- CloudBlob
- SrcCloudBlob
- SourceCloudBlob
- ICloudBlob
- SrcICloudBlob
- SourceICloudBlob
ParameterSets:
- Name: NativeBlob
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

String-keyed, string-valued destination metadata; omission preserves service
copy semantics.

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

P4 through P80 supported Page tiers, for a compatible Page source.
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

Named destination profile. Must agree with the native destination context.

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

### -RehydratePriority

High or Standard; requires a compatible Block tier.

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
- High
- Standard
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

Source Blob snapshot timestamp. Cannot be combined with VersionId.

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

### -SourceAccountKey

Base64 account key for the Files source, used only to sign a short-lived read-only
file SAS. Cannot be combined with UseSourceAccountKeyLookup unless a supplied SAS
takes precedence. Does not change destination authentication. Do not log this value.

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

### -SourceIfMatch

Blob source ETag match condition.

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

### -SourceIfModifiedSince

Blob source modified-since condition.

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

### -SourceIfNoneMatch

Blob source ETag non-match condition.

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

### -SourceIfUnmodifiedSince

Blob source unmodified-since condition.

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

### -SourceKeyName

Name of the source account key retrieved by explicit ARM lookup. Defaults to key1.
Use key2 when required for key rotation. Requires UseSourceAccountKeyLookup.

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

### -SourceKeyResourceGroup

Resource group containing the Files source account. Required with
UseSourceAccountKeyLookup; no automatic resource-group discovery is performed.

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

### -SourceKeySubscriptionId

ARM subscription containing the Files source account. Defaults to the source
profile subscription. Requires UseSourceAccountKeyLookup.

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

### -SourceSas

Existing source read SAS, with or without a leading question mark. Can accompany
named or native source inputs or an unsigned AbsoluteUri. Takes precedence over
source-key options and never falls back to keys after failure. Do not log this value.

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

### -SourceShareSnapshot

Source Files share snapshot. Cannot be combined with Blob selectors.

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

### -SrcBlob

Exact source blob name. Alias: SourceBlob.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases:
- SourceBlob
ParameterSets:
- Name: NamedBlob
  Position: 0
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
- Name: NativeContainer
  Position: 0
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -SrcContainer

Source blob container. Alias: SourceContainer.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases:
- SourceContainer
ParameterSets:
- Name: NamedBlob
  Position: Named
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -SrcContext

Explicit source context. Alias: SourceContext.

```yaml
Type: Microsoft.Azure.Azclips.Extensions.Storage.Customization.StorageContext
DefaultValue: ''
SupportsWildcards: false
Aliases:
- SourceContext
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

### -SrcDir

Native source directory. Alias: SourceDir. SrcFilePath is relative to it.

```yaml
Type: Microsoft.Azure.Azclips.Extensions.Storage.Customization.StorageFileDirectory
DefaultValue: ''
SupportsWildcards: false
Aliases:
- SourceDir
ParameterSets:
- Name: NativeDirectory
  Position: Named
  IsRequired: true
  ValueFromPipeline: true
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -SrcFile

Native source file. Alias: SourceFile.

```yaml
Type: Microsoft.Azure.Azclips.Extensions.Storage.Customization.StorageFile
DefaultValue: ''
SupportsWildcards: false
Aliases:
- SourceFile
ParameterSets:
- Name: NativeFile
  Position: Named
  IsRequired: true
  ValueFromPipeline: true
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -SrcFilePath

Source file path relative to the share or directory. Alias: SourceFilePath.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases:
- SourceFilePath
ParameterSets:
- Name: NamedFile
  Position: Named
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
- Name: NativeShare
  Position: Named
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
- Name: NativeDirectory
  Position: Named
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -SrcShare

Native source share. Alias: SourceShare.

```yaml
Type: Microsoft.Azure.Azclips.Extensions.Storage.Customization.StorageFileShare
DefaultValue: ''
SupportsWildcards: false
Aliases:
- SourceShare
ParameterSets:
- Name: NativeShare
  Position: Named
  IsRequired: true
  ValueFromPipeline: true
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -SrcShareName

Source Files share name. Alias: SourceShareName.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases:
- SourceShareName
ParameterSets:
- Name: NamedFile
  Position: Named
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -StandardBlobTier

Hot, Cool, Cold, or Archive, for a compatible Block source.

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

Destination tags. Alias: Tags.

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

Blob source SQL-like tag condition. Alias: SourceTagCondition. Blob-source
conditions are not supported for Files sources.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases:
- SourceTagCondition
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

### -UseSourceAccountKeyLookup

Explicitly authorize ARM ListKeys for the Files source account through its source
profile. Requires SourceKeyResourceGroup and permission to list source account keys.
Only the selected key is used to sign a short-lived read-only file SAS. This is
never an automatic fallback and does not change destination authentication.

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

Source Blob version selector.

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

Describe the operation without authentication, network requests, or copy start.
Runs the command in a mode that only reports what would happen without performing the actions.
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

### Microsoft.Azure.Azclips.Extensions.Storage.Customization.StorageContext

### Microsoft.Azure.Azclips.Extensions.Storage.Customization.StorageBlob

### Microsoft.Azure.Azclips.Extensions.Storage.Customization.StorageBlobContainer

### Microsoft.Azure.Azclips.Extensions.Storage.Customization.StorageFileShare

### Microsoft.Azure.Azclips.Extensions.Storage.Customization.StorageFileDirectory

### Microsoft.Azure.Azclips.Extensions.Storage.Customization.StorageFile

## OUTPUTS

### Microsoft.Azure.Azclips.Extensions.Storage.Customization.StorageBlob

## NOTES

## RELATED LINKS


- [New-AzStorageContext](New-AzStorageContext.md)
