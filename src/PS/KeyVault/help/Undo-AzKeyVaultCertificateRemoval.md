---
document type: cmdlet
external help file: Microsoft.Azure.Azclips.Cmdlets.KeyVault.dll-Help.xml
HelpUri: ""
Locale: en-US
Module Name: KeyVault
ms.date: 10/08/2026
PlatyPS schema version: 2024-05-01
title: Undo-AzKeyVaultCertificateRemoval
---

# Undo-AzKeyVaultCertificateRemoval

## SYNOPSIS

Recover a soft-deleted certificate and its versions.

## SYNTAX

### \_\_AllParameterSets

```
Undo-AzKeyVaultCertificateRemoval -Name <string> [-VaultName <string>] [-VaultUrl <string>]
 [-Profile <string>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

## ALIASES

None.

## DESCRIPTION

Recover a certificate within the soft-delete retention period in an existing Azure public-cloud vault. Requires certificates/recover permission. Specify either VaultName or the root HTTPS VaultUrl. Returns the public certificate bundle without fetching or exporting private-key material. Supports WhatIf and Confirm. A purged certificate or a certificate whose retention period has expired cannot be recovered.

## EXAMPLES

### Example 1

Recover a deleted certificate by name.

```powershell
Undo-AzKeyVaultCertificateRemoval -VaultName myvault -Name app-tls
```

### Example 2

Preview recovery without sending a request.

```powershell
Undo-AzKeyVaultCertificateRemoval -VaultName myvault -Name app-tls -WhatIf
```

## PARAMETERS

### -Confirm

Prompts you for confirmation before running the cmdlet.

```yaml
Type: System.Management.Automation.SwitchParameter
DefaultValue: ""
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
HelpMessage: ""
```

### -Name

Name of the soft-deleted certificate. Recovery applies to the certificate and its versions, not an individual version.

```yaml
Type: System.String
DefaultValue: ""
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
HelpMessage: ""
```

### -Profile

Named connection profile. When omitted, uses the active profile.

```yaml
Type: System.String
DefaultValue: ""
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
HelpMessage: ""
```

### -VaultName

Azure public-cloud vault name. Use either VaultName or VaultUrl.

```yaml
Type: System.String
DefaultValue: ""
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
HelpMessage: ""
```

### -VaultUrl

Root HTTPS URL of an Azure public-cloud vault, such as https://myvault.vault.azure.net/. Use instead of VaultName.

```yaml
Type: System.String
DefaultValue: ""
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
HelpMessage: ""
```

### -WhatIf

Runs the command in a mode that only reports what would happen without performing the actions.

```yaml
Type: System.Management.Automation.SwitchParameter
DefaultValue: ""
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
HelpMessage: ""
```

### CommonParameters

This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable,
-InformationAction, -InformationVariable, -OutBuffer, -OutVariable, -PipelineVariable,
-ProgressAction, -Verbose, -WarningAction, and -WarningVariable. For more information, see
[about_CommonParameters](https://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

## OUTPUTS

### Microsoft.Azure.Azclips.Extensions.KeyVault.Models.CertificateBundle

Public certificate data and metadata returned by the recovery operation, including identifiers, attributes and policy when supplied by the service. The command does not retrieve the backing secret or export a private key.

## NOTES

Returns the recovery response without polling for subsequent read availability. Supports name-based recovery only; legacy InputObject and CertificateName alias support are not included. Output follows the extension's REST-shaped certificate bundle, not the classic Az.KeyVault X509Certificate2-bearing object. CLI recovery, purge, deleted-certificate listing and backup restore are not implemented.

## RELATED LINKS

[Key Vault soft-delete overview](https://learn.microsoft.com/azure/key-vault/general/soft-delete-overview)
