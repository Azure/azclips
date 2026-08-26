---
document type: cmdlet
external help file: Microsoft.Azure.Azclips.Cmdlets.Storage.dll-Help.xml
HelpUri: ''
Locale: en-US
Module Name: Storage
ms.date: 08/26/2026
PlatyPS schema version: 2024-05-01
title: Start-AzStorageBlobCopy
---

# Start-AzStorageBlobCopy

## SYNOPSIS

Starts a server-side copy to an Azure Storage blob.

## SYNTAX

```
Start-AzStorageBlobCopy -AccountName <string> -DestContainer <string> -DestBlob <string>
 -AbsoluteUri <string> [-Profile <string>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

## DESCRIPTION

Starts a copy from an HTTP or HTTPS URL to a destination blob using Microsoft Entra authentication.

## EXAMPLES

### Copy a blob from a URL

```powershell
Start-AzStorageBlobCopy -AccountName mystorage -DestContainer files -DestBlob archive/report.csv -AbsoluteUri https://source.blob.core.windows.net/files/report.csv
```

## PARAMETERS

### -AbsoluteUri

The source blob or file URL.

### -AccountName

The destination storage account name.

### -DestBlob

The destination blob name.

### -DestContainer

The destination blob container name.

### -Profile

The named authentication profile to use.

## OUTPUTS

Microsoft.Azure.Azclips.Extensions.Storage.Models.StorageBlobCopyResult
