---
summary: Delete all versions of a secret without purging it.
status: preview
schema: 1.0.0
online version:
---

Description: Delete the named secret and all versions. Requires secrets/delete permission and explicit `--yes` confirmation. Output retains recovery metadata where returned by the service. The command never purges or bypasses purge protection. Recovery is not included in this release; use an existing recovery tool within the vault's retention period. A retained soft-deleted name cannot simply be reused as a new secret.

### Confirm deletion

```bash
az keyvault secret delete --vault-name myvault --name app-password --yes
```