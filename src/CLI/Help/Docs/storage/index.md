---
summary: Manage Azure Storage accounts and resources.
status: preview
schema: 1.0.0
online version:
---

Description: Manage Azure Storage. Currently scoped to storage account management via `az storage account create/show/list/delete`. Create provisions a storage account and supports SKU and kind selection, access tiers, network rules, encryption, managed identities, Azure Files identity-based authentication, and account-level immutability.

### Discover storage commands

```bash
az storage --help
```

### Create a storage account

```bash
az storage account create --resource-group myResourceGroup --name mystorageaccount --location eastus --sku Standard_LRS --kind StorageV2
```

### Show, list and delete storage accounts

```bash
az storage account show --resource-group myResourceGroup --name mystorageaccount
az storage account list --resource-group myResourceGroup
az storage account delete --resource-group myResourceGroup --name mystorageaccount
```
