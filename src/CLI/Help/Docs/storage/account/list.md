---
summary: List storage accounts.
status: preview
schema: 1.0.0
online version:
---

Description: List Azure Storage accounts. Supply `--resource-group` to list the accounts in one resource group, or omit it to list every storage account in the subscription. Results are returned as a flat array; paged responses from the service are followed automatically.

### List every storage account in the subscription

```bash
az storage account list
```

### List the storage accounts in a resource group

```bash
az storage account list --resource-group myResourceGroup
```
