---
summary: Manage Key Vault secrets.
status: preview
schema: 1.0.0
online version:
---

Description: Set, retrieve, list, update and delete secrets. Specify either `--vault-name` or the root HTTPS `--vault-url`. Commands return metadata by default. `show --as-plain-text` explicitly retrieves the value. Listings include all pages and never fetch values. Recovery, purge, backup and restore are not exposed; deletion still follows the vault's soft-delete policy. Use an existing recovery tool during the retention period when needed.

### List secret metadata

```bash
az keyvault secret list --vault-name myvault
```