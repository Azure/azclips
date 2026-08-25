---
document type: cmdlet
external help file: Microsoft.Azure.Azclips.Cmdlets.Storage.dll-Help.xml
HelpUri: ''
Locale: en-US
Module Name: Storage
ms.date: 08/25/2026
PlatyPS schema version: 2024-05-01
title: Get-AzStorageBlobContent
---

# Get-AzStorageBlobContent

## SYNOPSIS

Download an Azure Storage blob to a local file.

## SYNTAX

### Name

```
Get-AzStorageBlobContent -AccountName <string> -ContainerName <string> -BlobName <string>
 [-FilePath <string>] [-Force] [-SnapshotTime <DateTimeOffset>] [-VersionId <string>] [-CheckMd5]
 [-TagCondition <string>] [-AsJob] [-Profile <string>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

### Uri

```
Get-AzStorageBlobContent -AbsoluteUri <string> [-FilePath <string>] [-Force] [-CheckMd5]
 [-TagCondition <string>] [-AsJob] [-Profile <string>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

## ALIASES

## DESCRIPTION

Downloads a blob by storage name or Azure Blob URL. The destination may be a file or directory; when omitted, the blob name is used in the current directory. Snapshots, versions, tag conditions, and MD5 checking are supported. Existing files require confirmation unless `-Force` is supplied.

## EXAMPLES

### Download a blob

Download `archive.zip` from the `backups` container to the current directory.

```powershell
Get-AzStorageBlobContent -AccountName mystorageaccount -ContainerName backups -BlobName archive.zip -FilePath .\archive.zip
```

## PARAMETERS

### -AbsoluteUri

The absolute URI of the blob to download.

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
  ValueFromPipelineByPropertyName: true
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -AccountName

The name of the storage account.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases: []
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

### -AsJob

Runs the cmdlet as a background job.

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

### -BlobName

The name of the blob to download.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases:
- Blob
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

### -CheckMd5

Verifies the downloaded content against the blob's MD5 hash.

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

### -Confirm

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

### -ContainerName

The name of the blob container.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases:
- Container
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

### -FilePath

The local destination file path.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases:
- Destination
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

Overwrite the destination file if it already exists.

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

Name of a saved profile to use instead of the currently active profile.

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

### -SnapshotTime

The snapshot timestamp of the blob to download.

```yaml
Type: System.Nullable`1[System.DateTimeOffset]
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
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -TagCondition

An SQL-style condition that blob tags must satisfy.

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

The version ID of the blob to download.

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
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -WhatIf

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

Blob URIs supplied by property name.

## OUTPUTS

### Microsoft.Azure.Azclips.Extensions.Storage.Models.BlobTransferResult

The completed blob transfer, including source, destination, ETag, last-modified value, and content length when available.

## NOTES

Existing files require confirmation unless `-Force` is supplied.

## RELATED LINKS
