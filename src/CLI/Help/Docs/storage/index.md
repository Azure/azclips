---
summary: Manage Azure Storage accounts and resources.
status: preview
schema: 1.0.0
online version:
---

Description: Manage Azure Storage accounts and their data. Use `az storage account` to manage storage accounts. Use `az storage container` and `az storage blob` for Blob Storage data, and `az storage share`, `az storage directory`, and `az storage file` for Azure Files data. Data-plane commands authenticate with `--account-name` and `--account-key`, or with `--sas-token`.

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

### Upload a blob to a new container

```bash
az storage container create --account-name mystorageaccount --account-key $ACCOUNT_KEY --name mycontainer
az storage blob upload --account-name mystorageaccount --account-key $ACCOUNT_KEY --container-name mycontainer --name myblob.txt --file ./myfile.txt
```
