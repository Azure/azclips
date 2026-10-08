---
summary: Delete all versions of a secret without purging it.
status: preview
schema: 1.0.0
online version:
---

Description: Delete the named secret and all versions. Requires secrets/delete permission and explicit `--yes` confirmation. Output retains recovery metadata where returned by the service. The command never purges or bypasses purge protection. Use the PowerShell `Undo-AzKeyVaultSecretRemoval` cmdlet within the vault's retention period to recover the secret. A retained soft-deleted name cannot simply be reused as a new secret.

### Confirm deletion

```bash
az keyvault secret delete --vault-name myvault --name app-password --yes
```