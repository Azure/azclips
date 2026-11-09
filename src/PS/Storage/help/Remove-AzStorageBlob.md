---
document type: cmdlet
external help file: Microsoft.Azure.Azclips.Cmdlets.Storage.dll-Help.xml
HelpUri: ''
Locale: en-US
Module Name: Storage
ms.date: 10/22/2026
PlatyPS schema version: 2024-05-01
title: Remove-AzStorageBlob
---

# Remove-AzStorageBlob

## SYNOPSIS

Delete a selected native blob, version or snapshot, or a base blob's snapshots.

## SYNTAX

### NamePipeline (Default)

```
Remove-AzStorageBlob [-Blob] <string> [-Container] <string> -Context <StorageContext>
 [-SnapshotTime <DateTimeOffset>] [-VersionId <string>] [-DeleteSnapshot] [-Force] [-PassThru]
 [-TagCondition <string>] [-ServerTimeoutPerRequest <int>] [-Profile <string>] [-WhatIf] [-Confirm]
 [<CommonParameters>]
```

### BlobPipeline

```
Remove-AzStorageBlob -InputObject <StorageBlob> [-Context <StorageContext>] [-DeleteSnapshot]
 [-Force] [-PassThru] [-TagCondition <string>] [-ServerTimeoutPerRequest <int>] [-Profile <string>]
 [-WhatIf] [-Confirm] [<CommonParameters>]
```

### ContainerPipeline

```
Remove-AzStorageBlob [-Blob] <string> -ContainerObject <StorageBlobContainer>
 [-Context <StorageContext>] [-SnapshotTime <DateTimeOffset>] [-VersionId <string>]
 [-DeleteSnapshot] [-Force] [-PassThru] [-TagCondition <string>] [-ServerTimeoutPerRequest <int>]
 [-Profile <string>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

## ALIASES

None.

## DESCRIPTION

Deletes an exact resource using a public-Azure, profile-backed Entra StorageContext. Supply Blob, Container and Context, pipe a native StorageBlob, or pipe a native StorageBlobContainer with Blob. Names are literal; wildcard expansion is not performed.

Addressing, context conflicts and selectors are validated locally before ShouldProcess. WhatIf performs no remote request. Confirm and the session's confirmation preference control ShouldProcess; Force never bypasses that gate.

DeleteSnapshot deletes only the base blob's snapshots, retaining the base blob. Force on a base blob includes its snapshots without a separate snapshot prompt. If both switches are present, DeleteSnapshot takes precedence. Force on a selected snapshot/version never broadens deletion; DeleteSnapshot cannot be used with a selected resource.

Without either switch, the first request attempts normal deletion. Only HTTP 409 with the precise validated SnapshotsPresent code prompts whether to include snapshots. That ShouldContinue prompt runs on PowerShell's invocation thread after the first engine session is disposed. Approval retries once against the same resolved resource/context, retaining the tag condition and timeout; decline emits no success value. Other conflicts, missing resources, denied access and retry failures remain errors. Unlike the legacy broad any-409 prompt, unrelated conflicts never authorize broader deletion.

By default success produces no output. PassThru emits true only after the service accepts the requested deletion. Cancellation, WhatIf, declined confirmation and failures never emit a successful boolean. This is not a permanent purge command; account retention and soft-delete policies still apply.

## EXAMPLES

### Example 1

Preview removal without authentication or remote requests.

```powershell
$context = New-AzStorageContext -StorageAccountName account123
Remove-AzStorageBlob -Blob 'reports/month.csv' -Container content -Context $context -WhatIf
```

### Example 2

Delete only the base blob's snapshots, with standard confirmation.

```powershell
$context = New-AzStorageContext -StorageAccountName account123
Remove-AzStorageBlob -Blob 'reports/month.csv' -Container content -Context $context -DeleteSnapshot -Confirm
```

### Example 3

Delete a selected native version and emit true on acceptance.

```powershell
$context = New-AzStorageContext -StorageAccountName account123
$version = '2026-01-01T00:00:00.0000000Z'
$blob = Get-AzStorageBlob -Blob 'reports/month.csv' -Container content -Context $context -VersionId $version
$blob | Remove-AzStorageBlob -PassThru -Confirm
```

### Example 4

Explicitly include the base blob's snapshots. Force does not bypass Confirm.

```powershell
$context = New-AzStorageContext -StorageAccountName account123
Remove-AzStorageBlob -Blob 'reports/month.csv' -Container content -Context $context -Force -Confirm -PassThru
```

## PARAMETERS

### -Blob

Exact, nonempty blob name. Wildcards are not expanded. Required for named-container and native ContainerObject input.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: NamePipeline
  Position: 0
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
- Name: ContainerPipeline
  Position: 0
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -Confirm

Request standard ShouldProcess confirmation. Confirm:false does not disable the separate ShouldContinue prompt if a normal deletion reports SnapshotsPresent. Force also never bypasses this standard confirmation gate.

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

Container name, required with named Blob and Context input.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: NamePipeline
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

Native StorageBlobContainer supplied directly or through the pipeline. Uses its context; Blob identifies the exact resource within it. Legacy SDK containers are unsupported.

```yaml
Type: Microsoft.Azure.Azclips.Extensions.Storage.Customization.StorageBlobContainer
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: ContainerPipeline
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

Native StorageContext from New-AzStorageContext. Required for named inputs; optional for native resources, where it must agree with their account/profile. No Shared Key, connection string, destination SAS or automatic account-key fallback.

```yaml
Type: Microsoft.Azure.Azclips.Extensions.Storage.Customization.StorageContext
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: NamePipeline
  Position: Named
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
- Name: BlobPipeline
  Position: Named
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
- Name: ContainerPipeline
  Position: Named
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -DeleteSnapshot

Delete only snapshots of a base blob, retaining that blob. Takes precedence over Force. Cannot be combined with SnapshotTime, VersionId or a native input selecting a snapshot/version.

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

On a base blob, include its snapshots without the separate snapshot-conflict prompt. Does not override DeleteSnapshot or bypass ShouldProcess, WhatIf or Confirm. On a selected snapshot/version, deletion remains limited to that resource.

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

### -InputObject

Native StorageBlob, supplied directly or through the pipeline. Uses its name, container, context and snapshot/version selectors. Legacy SDK blob objects are unsupported.

```yaml
Type: Microsoft.Azure.Azclips.Extensions.Storage.Customization.StorageBlob
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: BlobPipeline
  Position: Named
  IsRequired: true
  ValueFromPipeline: true
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -PassThru

Emit true only after the requested deletion is accepted. Default is no output. Declined confirmation, WhatIf, cancellation and failed requests produce no success value.

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

Repository connection profile name, not a legacy DefaultProfile SDK object. An explicit value must agree with the native context/resource profile. A confirmed retry retains the resolved native context.

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

Positive per-request service timeout in seconds, retained on a confirmed retry. This is not an overall command or confirmation timeout.

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

Snapshot timestamp converted to UTC with round-trip precision. Explicit null is rejected rather than broadening deletion to the base blob. Cannot be combined with VersionId or DeleteSnapshot. Native blob input already carries its selector.

```yaml
Type: System.Nullable`1[System.DateTimeOffset]
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: NamePipeline
  Position: Named
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
- Name: ContainerPipeline
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

SQL-like tag condition applied to deletion and retained unchanged on an approved include-snapshots retry.

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

### -VersionId

Nonempty version ID selecting one exact version. Cannot be combined with SnapshotTime or DeleteSnapshot. Native blob input already carries its selector.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases: []
ParameterSets:
- Name: NamePipeline
  Position: Named
  IsRequired: false
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
- Name: ContainerPipeline
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

Report the validated target and requested deletion scope without opening an engine execution session or making remote requests, even when Force is supplied.

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

### Microsoft.Azure.Azclips.Extensions.Storage.Customization.StorageBlob

Native exact blob input, including its context and optional snapshot/version.

### Microsoft.Azure.Azclips.Extensions.Storage.Customization.StorageBlobContainer

Native container input, combined with the explicit Blob name.

## OUTPUTS

### System.Boolean

True only when PassThru is requested and deletion is accepted. Otherwise no success output.

## NOTES

Uses Blob REST API 2026-06-06 and the Storage Entra audience. A handled snapshot conflict is a typed control exception, never a success object or premature error stream entry. Retry failure is reported normally. A host that cannot prompt cannot implicitly approve inclusion of snapshots; for unattended deletion use Force only when broader deletion is intended.

## RELATED LINKS

[Get-AzStorageBlob](Get-AzStorageBlob.md)

[New-AzStorageContext](New-AzStorageContext.md)
