---
document type: cmdlet
external help file: Microsoft.Azure.Azclips.Cmdlets.Storage.dll-Help.xml
HelpUri: ''
Locale: en-US
Module Name: Storage
ms.date: 08/19/2026
PlatyPS schema version: 2024-05-01
title: Get-AzStorageBlobContent
---

# Get-AzStorageBlobContent

## SYNOPSIS

Download an Azure Storage blob to a local file.

## SYNTAX

### __AllParameterSets

```
Get-AzStorageBlobContent -AccountName <string> -ContainerName <string> -BlobName <string>
 -FilePath <string> [-Force] [-Profile <string>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

## ALIASES

This cmdlet has the following aliases,
  {{Insert list of aliases}}

## DESCRIPTION

Downloads the specified blob to a local path. The destination directory must exist. By default, the command fails if the destination file already exists; use `-Force` to replace it.

## EXAMPLES

### Download a blob

Download `archive.zip` from the `backups` container to the current directory.

```powershell
Get-AzStorageBlobContent -AccountName mystorageaccount -ContainerName backups -BlobName archive.zip -FilePath .\archive.zip
```

## PARAMETERS

### -AccountName

The name of the storage account.

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

### -BlobName

The name of the blob to download.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases:
- Blob
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

### -FilePath

The local destination file path.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases:
- Destination
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

## OUTPUTS

### Microsoft.Azure.Azclips.Extensions.Storage.Models.BlobTransferResult

The completed blob transfer, including source, destination, ETag, last-modified value, and content length when available.

## NOTES

The destination directory must exist before running the command.

## RELATED LINKS

