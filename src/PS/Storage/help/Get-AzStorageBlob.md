---
document type: cmdlet
external help file: Microsoft.Azure.Azclips.Cmdlets.Storage.dll-Help.xml
HelpUri: ''
Locale: en-US
Module Name: Storage
ms.date: 08/25/2026
PlatyPS schema version: 2024-05-01
title: Get-AzStorageBlob
---

# Get-AzStorageBlob

## SYNOPSIS

Gets blobs from an Azure Storage container.

## SYNTAX

### List (Default)

```
Get-AzStorageBlob -AccountName <string> -ContainerName <string> [-Prefix <string>]
 [-ContinuationToken <string>] [-MaxCount <int>] [-IncludeDeleted] [-IncludeVersion]
 [-IncludeTag] [-Profile <string>] [<CommonParameters>]
```

### Get

```
Get-AzStorageBlob -BlobName <string> -AccountName <string> -ContainerName <string>
 [-IncludeTag] [-Profile <string>] [<CommonParameters>]
```

## ALIASES

## DESCRIPTION

Gets one blob's properties or lists blobs in a container using Microsoft Entra authentication.

## EXAMPLES

### Get one blob

```powershell
Get-AzStorageBlob -AccountName mystorage -ContainerName files -BlobName reports/report.csv
```

### List blobs by prefix

```powershell
Get-AzStorageBlob -AccountName mystorage -ContainerName files -Prefix reports/ -MaxCount 100
```

## PARAMETERS

### -AccountName

The name of the storage account.

### -BlobName

The name of the blob to get.

### -ContainerName

The name of the blob container.

### -ContinuationToken

The continuation marker returned by a previous list operation.

### -IncludeDeleted

Includes deleted blobs in list results.

### -IncludeTag

Includes blob tags.

### -IncludeVersion

Includes blob versions in list results.

### -MaxCount

The maximum number of blobs to return.

### -Prefix

Filters list results by blob name prefix.

### -Profile

The saved profile to use instead of the active profile.

### CommonParameters

This cmdlet supports the common parameters. For more information, see
[about_CommonParameters](https://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

## OUTPUTS

### Microsoft.Azure.Azclips.Extensions.Storage.Models.StorageBlob

Blob properties and metadata.

## NOTES

## RELATED LINKS
