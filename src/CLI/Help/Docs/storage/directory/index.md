---
summary: Manage directories in Azure file shares.
status: preview
schema: 1.0.0
online version:
---

Description: Create, show, and delete directories in a file share. List the content of a directory with `az storage file list`.

### Create a directory

```bash
az storage directory create --account-name mystorageaccount --account-key $ACCOUNT_KEY --share-name myshare --name mydir
```

### Delete an empty directory

```bash
az storage directory delete --account-name mystorageaccount --account-key $ACCOUNT_KEY --share-name myshare --name mydir
```
