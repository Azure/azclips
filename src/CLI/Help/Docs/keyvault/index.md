---
summary: Manage Key Vault data-plane objects.
status: preview
schema: 1.0.0
online version:
---

Description: Manage secrets and certificates in an existing Azure public-cloud vault. Sign in and select a profile with the required data-plane permissions. Vault creation, other clouds, standalone keys, CLI recovery, purge and backup/restore are not included. PowerShell provides `Undo-AzKeyVaultSecretRemoval` and `Undo-AzKeyVaultCertificateRemoval` for soft-delete recovery.

### List secrets in a vault

```bash
az keyvault secret list --vault-name myvault
```
