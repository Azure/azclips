---
document type: cmdlet
external help file: Microsoft.Azure.Azclips.Cmdlets.Storage.dll-Help.xml
HelpUri: ''
Locale: en-US
Module Name: Storage
ms.date: 08/27/2026
PlatyPS schema version: 2024-05-01
title: Start-AzStorageBlobCopy
---

# Start-AzStorageBlobCopy

## SYNOPSIS

Starts a server-side copy to an Azure Storage blob.

## SYNTAX

### __AllParameterSets

```
Start-AzStorageBlobCopy -AccountName <string> -DestContainer <string> -DestBlob <string>
 -AbsoluteUri <string> [-Profile <string>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

## ALIASES

Start-CopyAzureStorageBlob

## DESCRIPTION

Starts a copy from an HTTP or HTTPS URL to a destination blob using Microsoft Entra authentication.

## EXAMPLES

### Copy a blob from a URL

Copies a blob from a source URL to the specified destination.

```powershell
Start-AzStorageBlobCopy -AccountName mystorage -DestContainer files -DestBlob archive/report.csv -AbsoluteUri https://source.blob.core.windows.net/files/report.csv
```

## PARAMETERS

### -AbsoluteUri

The source blob or file URL.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases:
- SrcUri
- SourceUri
ParameterSets:
- Name: (All)
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

The destination storage account name.

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

### -DestBlob

The destination blob name.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases:
- DestinationBlob
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

### -DestContainer

The destination blob container name.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases:
- DestinationContainer
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

### -Profile

The named authentication profile to use.

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

### System.String

Accepts the source URI from a pipeline object's `AbsoluteUri`, `SrcUri`, or `SourceUri` property.

## OUTPUTS

### Microsoft.Azure.Azclips.Extensions.Storage.Models.StorageBlobCopyResult

The result of the server-side copy request.

## NOTES

This command starts the copy operation; Azure Storage completes it asynchronously.

## RELATED LINKS
