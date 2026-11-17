---
summary: Show the properties of a storage account.
status: preview
schema: 1.0.0
online version:
---

Description: Get the properties of an Azure Storage account, including its SKU, kind, location, endpoints, network rules, encryption settings, and provisioning state. Use `--expand geoReplicationStats` to include geo-replication status, which reports the replication state and whether the account can fail over.

### Show a storage account

```bash
az storage account show --resource-group myResourceGroup --name mystorageaccount
```

### Show a storage account with geo-replication statistics

```bash
az storage account show --resource-group myResourceGroup --name mystorageaccount --expand geoReplicationStats
```
