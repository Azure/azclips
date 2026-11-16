---
summary: Create a file share.
status: preview
schema: 1.0.0
online version:
---

Description: Create a file share in a storage account. Use `--quota` to set the maximum share size in GiB.

### Create a file share

```bash
az storage share create --account-name mystorageaccount --account-key $ACCOUNT_KEY --name myshare
```

### Create a file share with a 100 GiB quota

```bash
az storage share create --account-name mystorageaccount --account-key $ACCOUNT_KEY --name myshare --quota 100
```
