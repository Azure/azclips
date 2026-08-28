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

### __AllParameterSets

```
New-AzStorageBlobSASToken [-BlobName] <string> -AccountName <string> -ContainerName <string>
 -Permission <string> -ExpiryTime <DateTimeOffset> [-StartTime <DateTimeOffset>]
 [-IPAddressOrRange <string>] [-HttpsOnly] [-FullUri] [-Profile <string>] [<CommonParameters>]
```

## ALIASES

## DESCRIPTION

Generates a blob SAS using Microsoft Entra authentication. The signed-in identity must be allowed
to request a user delegation key.

## EXAMPLES

### Generate a read-only SAS

Generates an HTTPS-only SAS that grants read access for one hour.

```powershell
$expiry = [DateTime]::UtcNow.AddHours(1)
New-AzStorageBlobSASToken -AccountName mystorage -ContainerName files -BlobName report.csv -Permission r -ExpiryTime $expiry -HttpsOnly
```

### Return the complete blob URI

Returns the complete blob URI instead of only the SAS query string.

```powershell
$expiry = [DateTime]::UtcNow.AddHours(1)
New-AzStorageBlobSASToken -AccountName mystorage -ContainerName files -BlobName report.csv -Permission rw -ExpiryTime $expiry -FullUri
```

## PARAMETERS

### -AccountName

The storage account name.

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

The blob name.

```yaml
Type: System.String
DefaultValue: ''
SupportsWildcards: false
Aliases:
- Blob
ParameterSets:
- Name: (All)
  Position: 0
  IsRequired: true
  ValueFromPipeline: false
  ValueFromPipelineByPropertyName: false
  ValueFromRemainingArguments: false
DontShow: false
AcceptedValues: []
HelpMessage: ''
```

### -ContainerName

The blob container name.

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

### -ExpiryTime

The time when the SAS expires. The expiry cannot be more than seven days from the current time.

```yaml
Type: System.DateTimeOffset
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

### -FullUri

Returns the complete blob URI instead of only the SAS query string.

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

### -HttpsOnly

Allows the SAS to be used only over HTTPS.

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

### -IPAddressOrRange

The IPv4 address or inclusive IPv4 range allowed to use the SAS.

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

### -Permission

The blob permissions to grant, such as `r` or `rw`.

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

### -StartTime

The time when the SAS becomes valid. Defaults to five minutes before the current time.

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

### CommonParameters

This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable,
-InformationAction, -InformationVariable, -OutBuffer, -OutVariable, -PipelineVariable,
-ProgressAction, -Verbose, -WarningAction, and -WarningVariable. For more information, see
[about_CommonParameters](https://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

## OUTPUTS

### System.String

The SAS query string or complete blob URI.

## NOTES

## RELATED LINKS

