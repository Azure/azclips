---
document type: cmdlet
external help file: Microsoft.Azure.Azclips.Cmdlets.Storage.dll-Help.xml
HelpUri: ''
Locale: en-US
Module Name: Storage
ms.date: 08/28/2026
PlatyPS schema version: 2024-05-01
title: New-AzStorageBlobSASToken
---

# New-AzStorageBlobSASToken

## SYNOPSIS

Generates a user delegation SAS for an Azure Storage blob.

## SYNTAX

```
New-AzStorageBlobSASToken [-BlobName] <string> -AccountName <string> -ContainerName <string>
 -Permission <string> -ExpiryTime <datetime> [-StartTime <datetime>]
 [-IPAddressOrRange <string>] [-HttpsOnly] [-FullUri] [-Profile <string>] [<CommonParameters>]
```

## DESCRIPTION

Generates a blob SAS using Microsoft Entra authentication. The signed-in identity must be allowed
to request a user delegation key.

## EXAMPLES

### Generate a read-only SAS

```powershell
New-AzStorageBlobSASToken -AccountName mystorage -ContainerName files -BlobName report.csv -Permission r -ExpiryTime (Get-Date).AddHours(1) -HttpsOnly
```

### Return the complete blob URI

```powershell
New-AzStorageBlobSASToken -AccountName mystorage -ContainerName files -BlobName report.csv -Permission rw -ExpiryTime (Get-Date).AddHours(1) -FullUri
```

## PARAMETERS

### -AccountName

The storage account name.

### -BlobName

The blob name.

### -ContainerName

The blob container name.

### -ExpiryTime

The time when the SAS expires. The expiry cannot be more than seven days from the current time.

### -FullUri

Returns the complete blob URI instead of only the SAS query string.

### -HttpsOnly

Allows the SAS to be used only over HTTPS.

### -IPAddressOrRange

The IPv4 address or inclusive IPv4 range allowed to use the SAS.

### -Permission

The blob permissions to grant, such as `r` or `rw`.

### -Profile

The named authentication profile to use.

### -StartTime

The time when the SAS becomes valid. Defaults to five minutes before the current time.

### CommonParameters

This cmdlet supports the common parameters.

## OUTPUTS

### System.String

The SAS query string or complete blob URI.
