---
summary: Create a blob container.
status: preview
schema: 1.0.0
online version:
---

Description: Create a container in a storage account. Containers are private by default; use `--public-access` to allow anonymous read access to blobs or to the whole container.

### Create a private container

```bash
az storage container create --account-name mystorageaccount --account-key $ACCOUNT_KEY --name mycontainer
```

### Create a container with anonymous blob read access

```bash
az storage container create --account-name mystorageaccount --account-key $ACCOUNT_KEY --name publiccontainer --public-access blob
```
