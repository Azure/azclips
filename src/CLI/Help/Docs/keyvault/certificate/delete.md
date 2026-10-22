---
summary: Soft-delete all versions of a certificate.
status: preview
schema: 1.0.0
online version:
---

Description: Delete the name, all versions and associated policy. Requires certificates/delete and explicit `--yes`. Returns service recovery metadata without deleted-state polling. Never purges or alters vault retention settings. Recovery is not exposed in the CLI.

### Confirm deletion

```bash
az keyvault certificate delete --vault-name myvault --name app-tls --yes
```