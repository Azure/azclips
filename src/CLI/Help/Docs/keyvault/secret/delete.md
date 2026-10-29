---
summary: Delete all versions of a secret without purging it.
status: preview
schema: 1.0.0
online version:
---

Description: Delete the named secret and all versions. Requires secrets/delete permission and explicit `--yes` confirmation. Output retains recovery metadata where returned by the service. The command never purges or bypasses purge protection. Recovery is not exposed in the CLI. A retained soft-deleted name cannot simply be reused as a new secret.

Purge is a known gap in this preview. A CLI secret purge command is not exposed.

### Confirm deletion

```bash
az keyvault secret delete --vault-name myvault --name app-password --yes
```