---
summary: Manage Azure Storage accounts.
status: preview
schema: 1.0.0
online version:
---

Description: Create and manage Azure Storage accounts. Backed by `az storage account create/show/list/delete`, which mirror the corresponding Azure CLI verbs. Create supports SKU and kind selection, access tiers, TLS and public access settings, network rules, customer-managed encryption keys, managed identities, Azure Files identity-based authentication, SAS and key policies, and account-level immutability.

### Create a storage account

```bash
az storage account create --resource-group myResourceGroup --name mystorageaccount --location eastus --sku Standard_LRS --kind StorageV2
```

### Show a storage account

```bash
az storage account show --resource-group myResourceGroup --name mystorageaccount
```

### List storage accounts

```bash
az storage account list --resource-group myResourceGroup
```

### Delete a storage account

```bash
az storage account delete --resource-group myResourceGroup --name mystorageaccount
```

### Create a storage account with restricted network access

```bash
az storage account create --resource-group myResourceGroup --name mystorageaccount --location eastus --sku Standard_LRS --kind StorageV2 --default-action Deny --bypass Logging Metrics AzureServices --ip-address 13.64.0.0/16
```

### List account keys

```bash
az storage account keys list --resource-group myResourceGroup --account-name mystorageaccount
```
