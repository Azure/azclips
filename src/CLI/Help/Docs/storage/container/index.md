---
summary: Manage blob containers.
status: preview
schema: 1.0.0
online version:
---

Description: Create and delete blob containers in a storage account. Authenticate with `--account-name` and `--account-key`, or with `--sas-token`.

### Create a container

```bash
az storage container create --account-name mystorageaccount --account-key $ACCOUNT_KEY --name mycontainer
```

### Delete a container

```bash
az storage container delete --account-name mystorageaccount --account-key $ACCOUNT_KEY --name mycontainer
```
