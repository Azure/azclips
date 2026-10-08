---
summary: Soft-delete all versions of a certificate.
status: preview
schema: 1.0.0
online version:
---

Description: Delete the name, all versions and associated policy. Requires certificates/delete and explicit `--yes`. Returns service recovery metadata without deleted-state polling. Never purges or alters vault retention settings. Use the PowerShell `Undo-AzKeyVaultCertificateRemoval` cmdlet within the retention period to recover the certificate.

### Confirm deletion

```bash
az keyvault certificate delete --vault-name myvault --name app-tls --yes
```