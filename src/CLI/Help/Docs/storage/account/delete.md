---
summary: Delete a storage account.
status: preview
schema: 1.0.0
online version:
---

Description: Delete an Azure Storage account and all of the data it contains. This cannot be undone. The command deletes immediately without prompting for confirmation, so there is no `--yes` option. The command returns no output on success.

### Delete a storage account

```bash
az storage account delete --resource-group myResourceGroup --name mystorageaccount
```
